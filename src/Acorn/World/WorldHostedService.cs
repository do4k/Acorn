using System.Diagnostics;
using Acorn.Infrastructure.Telemetry;
using Acorn.Options;
using Acorn.World.Services.Marriage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.World;

internal class WorldHostedService : BackgroundService
{
    private readonly TimeSpan _tickInterval;
    private readonly WorldState _world;
    private readonly IMarriageService _marriageService;
    private readonly ILogger<WorldHostedService> _logger;
    private readonly AcornMetrics _metrics;
    private readonly int _tickSpanSampleEvery;
    private long _tickNumber;

    public WorldHostedService(IOptions<ServerOptions> options, WorldState world, IMarriageService marriageService, ILogger<WorldHostedService> logger, AcornMetrics metrics)
    {
        _tickInterval = TimeSpan.FromMilliseconds(options.Value.TickRate);
        _world = world;
        _marriageService = marriageService;
        _logger = logger;
        _metrics = metrics;
        _tickSpanSampleEvery = Math.Max(1, options.Value.WorldTickSpanSampleEvery);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.WorldTickStarted(_tickInterval.TotalMilliseconds);

        // PeriodicTimer serialises ticks: the next interval only begins after the current
        // iteration completes, so overlapping world ticks cannot occur.
        using var timer = new PeriodicTimer(_tickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // Full-fidelity duration lives in the MapTickDuration histogram recorded below;
            // spans are sampled so the trace buffer keeps room for packet traces (#137).
            var isSampled = ShouldEmitTickSpan(Interlocked.Increment(ref _tickNumber), _tickSpanSampleEvery);

            var sw = Stopwatch.StartNew();
            Activity? activity = null;
            try
            {
                if (isSampled)
                {
                    activity = AcornActivitySource.Instance.StartActivity("world.tick", ActivityKind.Internal);
                    activity?.SetTag("acorn.maps.count", _world.Maps.Count);
                }

                var tickTasks = _world
                    .Maps
                    .Select(x => x.Value.Tick());

                await Task.WhenAll(tickTasks);

                // Process wedding ceremonies on maps that have active weddings
                var weddingTasks = _world
                    .Maps
                    .Where(x => x.Value.Wedding != null)
                    .Select(x => _marriageService.ProcessWeddingTickAsync(x.Value));

                await Task.WhenAll(weddingTasks);
            }
            catch (Exception ex)
            {
                // An unsampled tick that blew up still deserves a trace.
                activity ??= AcornActivitySource.Instance.StartActivity("world.tick", ActivityKind.Internal);
                if (activity is not null)
                {
                    activity.SetTag("acorn.maps.count", _world.Maps.Count);
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity.AddException(ex);
                }

                _logger.LogError(ex, "Error during world tick");
            }
            finally
            {
                sw.Stop();
                _metrics.MapTickDuration.Record(sw.Elapsed.TotalMilliseconds);
                activity?.Dispose();
            }
        }
    }

    /// <summary>
    ///     Whether the tick with this (1-based) sequence number should open a span.
    ///     Called with the configured sampling interval; intervals of 1 span every tick.
    /// </summary>
    internal static bool ShouldEmitTickSpan(long tickNumber, int sampleEvery)
    {
        return tickNumber % Math.Max(1, sampleEvery) == 0;
    }
}
