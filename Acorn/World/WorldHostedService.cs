using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Acorn.World;

internal class WorldHostedService : IHostedService
{
    public WorldHostedService(IConfiguration configuration)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // todo: world tick events, npcs moving etc...
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}