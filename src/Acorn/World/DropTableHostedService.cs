using Acorn.Database.Repository;
using Acorn.Game.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acorn.World;

/// <summary>
///     Hosted service that loads NPC drop tables at server startup
/// </summary>
internal class DropTableHostedService : IHostedService
{
    private readonly DropFileTextLoader _dropFileLoader;
    private readonly ILogger<DropTableHostedService> _logger;
    private readonly ILootService _lootService;

    public DropTableHostedService(
        ILootService lootService,
        DropFileTextLoader dropFileLoader,
        ILogger<DropTableHostedService> logger)
    {
        _lootService = lootService;
        _dropFileLoader = dropFileLoader;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading NPC drop tables...");

        var dropFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "drops.txt");
        _dropFileLoader.LoadDrops(_lootService, dropFilePath);

        var globalDropFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "global_drops.txt");
        _dropFileLoader.LoadGlobalDrops(_lootService, globalDropFilePath);

        // Freeze loot tables once loaded - the hot-path lookup becomes immutable.
        _lootService.Seal();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}