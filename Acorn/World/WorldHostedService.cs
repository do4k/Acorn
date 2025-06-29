using System.Timers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Acorn.World;

internal class WorldHostedService : IHostedService
{
    private readonly WorldState _world;
    private readonly System.Timers.Timer _timer;

    public WorldHostedService(IConfiguration configuration, WorldState world)
    {
        _world = world;
        _timer = new(1000);
        _timer.Elapsed += Tick!;
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

    private void Tick(object sender, ElapsedEventArgs args)
    {
        var tickTasks = _world
            .Maps
            .Select(x => x.Value.Tick());
        
        Task.WhenAll(tickTasks).GetAwaiter().GetResult();
    }
}