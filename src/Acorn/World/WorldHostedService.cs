using System.Timers;
using Acorn.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Timer = System.Timers.Timer;

namespace Acorn.World;

internal class WorldHostedService : IHostedService
{
    private readonly Timer _timer;
    private readonly WorldState _world;
    private readonly ILogger<WorldHostedService> _logger;

    public WorldHostedService(IOptions<ServerOptions> options, WorldState world, ILogger<WorldHostedService> logger)
    {
        _world = world;
        _logger = logger;
        _timer = new Timer(options.Value.TickRate);
        _timer.Elapsed += OnTick;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Stop();
        return Task.CompletedTask;
    }

    private async void OnTick(object sender, ElapsedEventArgs args)
    {
        try
        {
            var tickTasks = _world
                .Maps
                .Select(x => x.Value.Tick());

            await Task.WhenAll(tickTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during world tick");
        }
    }
}