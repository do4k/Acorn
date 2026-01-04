using System.Timers;
using Acorn.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Timer = System.Timers.Timer;

namespace Acorn.World;

internal class WorldHostedService : IHostedService
{
    private readonly Timer _timer;
    private readonly WorldState _world;

    public WorldHostedService(IOptions<ServerOptions> options, WorldState world)
    {
        _world = world;
        _timer = new Timer(options.Value.TickRate);
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