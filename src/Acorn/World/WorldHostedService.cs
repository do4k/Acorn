using System.Timers;
using Acorn.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Acorn.World;

internal class WorldHostedService : IHostedService
{
    private readonly WorldState _world;
    private readonly System.Timers.Timer _timer;

    public WorldHostedService(IOptions<ServerOptions> options, WorldState world)
    {
        _world = world;
        _timer = new(options.Value.TickRate);
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