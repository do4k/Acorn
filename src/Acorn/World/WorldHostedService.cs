using System.Timers;
using Acorn.Infrastructure.Telemetry;
using Acorn.Options;
using Acorn.World.Services.Marriage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Timer = System.Timers.Timer;

namespace Acorn.World;

internal class WorldHostedService : IHostedService
{
    private readonly Timer _timer;
    private readonly WorldState _world;
    private readonly IMarriageService _marriageService;
    private readonly ILogger<WorldHostedService> _logger;
    private readonly AcornMetrics _metrics;

    public WorldHostedService(IOptions<ServerOptions> options, WorldState world, IMarriageService marriageService, ILogger<WorldHostedService> logger, AcornMetrics metrics)
    {
        _world = world;
        _marriageService = marriageService;
        _logger = logger;
        _metrics = metrics;
        _timer = new Timer(options.Value.TickRate);
        _timer.Elapsed += OnTick;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.WorldTickStarted(_timer.Interval);
        _timer.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.WorldTickStopped();
        _timer.Stop();
        return Task.CompletedTask;
    }

    private async void OnTick(object? sender, ElapsedEventArgs args)
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