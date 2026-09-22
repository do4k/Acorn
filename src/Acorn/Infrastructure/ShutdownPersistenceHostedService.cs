using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.World;
using Acorn.World.Services.Quest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure;

/// <summary>
///     Persists every online character (and their quest progress) when the host is
///     stopping, so a graceful shutdown - SIGTERM, <c>docker stop</c> or <c>$shutdown</c> -
///     never loses state for players whose sockets close without the normal
///     disconnect cleanup running.
/// </summary>
/// <remarks>
///     Registered last so the host stops it first, while the world state and all
///     connections are still intact. The per-character disconnect save in
///     <see cref="Net.ConnectionHandler" /> remains the primary path; this service
///     is a catch-all, and writing the same row twice is harmless.
/// </remarks>
public class ShutdownPersistenceHostedService(
    WorldState worldState,
    IServiceScopeFactory scopeFactory,
    ILogger<ShutdownPersistenceHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Idle until the host requests shutdown, then persist. StopAsync waits for
        // this method to complete, so the process does not exit mid-save.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected: cancellation is the shutdown signal.
        }

        await PersistAllOnlineCharactersAsync();
    }

    private async Task PersistAllOnlineCharactersAsync()
    {
        var players = worldState.Players.Values
            .Where(p => p.Character is not null)
            .ToList();

        if (players.Count == 0) return;

        logger.LogInformation("Persisting {Count} online character(s) for shutdown", players.Count);

        // Same pattern as disconnect cleanup: a fresh scope keeps shutdown writes off
        // any DbContext a live connection might still be using.
        using var scope = scopeFactory.CreateScope();
        var characters = scope.ServiceProvider.GetRequiredService<IDbRepository<Character>>();
        var characterMapper = scope.ServiceProvider.GetRequiredService<ICharacterMapper>();
        var questService = scope.ServiceProvider.GetRequiredService<IQuestService>();

        foreach (var player in players)
        {
            try
            {
                await characters.UpdateAsync(characterMapper.ToDatabase(player.Character!));
                await questService.SaveQuestProgress(player.Character!);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist character {Character} during shutdown",
                    player.Character!.Name);
            }
        }

        logger.LogInformation("Shutdown persistence complete");
    }
}
