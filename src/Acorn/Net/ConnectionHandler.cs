using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.World;
using Acorn.World.Services.Party;
using Acorn.World.Services.Quest;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net;

/// <summary>
/// Handles new player connections from any transport (TCP or WebSocket).
/// Responsible for creating PlayerState instances, adding them to the world,
/// and cleaning up when players disconnect.
/// </summary>
public class ConnectionHandler(
    ILogger<ConnectionHandler> logger,
    WorldState worldState,
    IDbRepository<Character> characterRepository,
    ICharacterMapper characterMapper,
    ISessionGenerator sessionGenerator,
    PlayerStateFactory playerStateFactory,
    IPartyService partyService,
    IQuestService questService
)
{
    /// <summary>
    /// Creates a PlayerState for a new connection, adds it to the world, and returns the session ID.
    /// </summary>
    public int AcceptConnection(ICommunicator communicator)
    {
        var sessionId = sessionGenerator.Generate();

        var playerState = playerStateFactory.CreatePlayerState(communicator, sessionId,
            player => OnClientDisposedAsync(player, sessionId));

        var added = worldState.TryAddPlayer(sessionId, playerState);
        if (!added)
        {
            logger.LogWarning("Failed to add player session {SessionId} to world state", sessionId);
        }

        logger.LogInformation("Connection accepted. {PlayersConnected} players connected",
            worldState.Players.Count);
        UpdateConnectedCount();

        return sessionId;
    }

    private async Task OnClientDisposedAsync(PlayerState player, int sessionId)
    {
        // Cancel any pending warp
        player.WarpSession = null;

        // Cancel any pending trade
        if (player.TradeSession != null)
        {
            var partner = player.CurrentMap?.Players.Values.FirstOrDefault(p =>
                p.SessionId == player.TradeSession?.PartnerId);

            if (partner != null)
            {
                partner.TradeSession = null;
                partner.PendingTradeRequestFromPlayerId = null;
                await partner.Send(new TradeCloseServerPacket());
            }

            player.TradeSession = null;
            player.PendingTradeRequestFromPlayerId = null;
        }

        // Clean up party membership
        await partyService.HandlePlayerDisconnect(player);

        if (player.Character is not null && player.CurrentMap is not null)
        {
            await player.CurrentMap.NotifyLeave(player);
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character));
            await questService.SaveQuestProgress(player.Character);
        }

        worldState.TryRemovePlayer(sessionId, out _);
        logger.LogInformation(
            "Player disconnected (Session {SessionId}, Character: {Character}). {PlayersConnected} players remaining",
            sessionId, player.Character?.Name ?? "none", worldState.Players.Count);
        UpdateConnectedCount();
    }

    private void UpdateConnectedCount()
    {
        Console.Title = $"Acorn Server ({worldState.Players.Count} Connected)";
    }
}
