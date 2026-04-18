using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Players;

/// <summary>
///     Handles #find command - looks up whether a player is online.
///     Responds with Net242 (online, different map), Pong (online, same map), or Ping (offline).
/// </summary>
[RequiresCharacter]
internal class PlayersAcceptClientPacketHandler(WorldState worldState)
    : IPacketHandler<PlayersAcceptClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, PlayersAcceptClientPacket packet)
    {
        var targetName = packet.Name?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(targetName))
        {
            return;
        }

        var target = worldState.Players.Values
            .FirstOrDefault(p => p.Character?.Name?.Equals(targetName, StringComparison.InvariantCultureIgnoreCase) == true);

        if (target?.Character is null)
        {
            // Player is offline
            await playerState.Send(new PlayersPingServerPacket { Name = targetName });
            return;
        }

        if (target.Character.Map == playerState.Character!.Map)
        {
            // Player is on the same map
            await playerState.Send(new PlayersPongServerPacket { Name = targetName });
        }
        else
        {
            // Player is online but on a different map
            await playerState.Send(new PlayersNet242ServerPacket { Name = targetName });
        }
    }
}
