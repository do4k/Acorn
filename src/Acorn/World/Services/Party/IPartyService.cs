using Acorn.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Party;

/// <summary>
/// Service for managing party operations.
/// </summary>
public interface IPartyService
{
    /// <summary>
    /// Send a party invite or join request to another player.
    /// </summary>
    Task RequestParty(PlayerState requester, int targetSessionId, PartyRequestType type);

    /// <summary>
    /// Accept an incoming party request, creating or joining a party.
    /// </summary>
    Task AcceptPartyRequest(PlayerState player, int requesterSessionId, PartyRequestType type);

    /// <summary>
    /// Remove a player from a party. If playerId == targetSessionId, it's a leave.
    /// If leader kicks and only 2 members remain, disband.
    /// </summary>
    Task RemoveFromParty(PlayerState player, int targetSessionId);

    /// <summary>
    /// Send the current party member list to the requesting player.
    /// </summary>
    Task RefreshPartyList(PlayerState player);

    /// <summary>
    /// Broadcast HP update to all party members.
    /// </summary>
    Task BroadcastHpUpdate(PlayerState player);

    /// <summary>
    /// Distribute experience among party members on the same map.
    /// </summary>
    Task DistributeExp(int killerSessionId, int totalExp, int mapId);

    /// <summary>
    /// Send a chat message to all party members.
    /// </summary>
    Task SendPartyMessage(PlayerState sender, string message);

    /// <summary>
    /// Get the party a player belongs to, or null.
    /// </summary>
    Party? GetPlayerParty(int sessionId);

    /// <summary>
    /// Clean up party membership when a player disconnects.
    /// </summary>
    Task HandlePlayerDisconnect(PlayerState player);
}
