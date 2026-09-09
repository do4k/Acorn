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

    public WorldHostedService(IOptions<ServerOptions> options, WorldState world, IMarriageService marriageService, ILogger<WorldHostedService> logger, AcornMetrics metrics)
    {
        _tickInterval = TimeSpan.FromMilliseconds(options.Value.TickRate);
        _world = world;
        _marriageService = marriageService;
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.WorldTickStarted(_tickInterval.TotalMilliseconds);

        // PeriodicTimer serialises ticks: the next interval only begins after the current
        // iteration completes, so overlapping world ticks cannot occur.
        using var timer = new PeriodicTimer(_tickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
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
                _logger.LogError(ex, "Error during world tick");
            }
            finally
            {
                sw.Stop();
                _metrics.MapTickDuration.Record(sw.Elapsed.TotalMilliseconds);
            }
        }
    }
}
