using System.Timers;
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

    public WorldHostedService(IOptions<ServerOptions> options, WorldState world, IMarriageService marriageService, ILogger<WorldHostedService> logger)
    {
        _world = world;
        _marriageService = marriageService;
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

    private async void OnTick(object? sender, ElapsedEventArgs args)
    {
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
    }
}