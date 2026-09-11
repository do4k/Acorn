using Acorn.Extensions;
using Acorn.World.Services.Map;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
public class EmoteReportClientPacketHandler(IMapTileService tileService) : IPacketHandler<EmoteReportClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, EmoteReportClientPacket packet)
    {
        if (playerState.CurrentMap is null || playerState.Character is null)
        {
            return;
        }

        if (!IsValidEmote(packet.Emote))
        {
            return;
        }

        var origin = playerState.Character.AsCoords();

        // Emotes are only visible to players within client render range.
        var recipients = playerState.CurrentMap.Players.Values
            .Where(player => player.SessionId != playerState.SessionId
                             && player.Character is not null
                             && tileService.InClientRange(origin, player.Character.AsCoords()));

        await Task.WhenAll(recipients.Select(player => player.Send(new EmotePlayerServerPacket
        {
            PlayerId = playerState.SessionId,
            Emote = packet.Emote
        })));
    }

    /// <summary>
    ///     Only the standard player emotes (1-10), Drunk and Playful may be sent by
    ///     clients. Trade, LevelUp and Bard are server-triggered only. Mirrors eoserv
    ///     Emote_Report.
    /// </summary>
    internal static bool IsValidEmote(Emote emote)
    {
        var value = (int)emote;
        return (value >= 1 && value <= 10) || emote == Emote.Drunk || emote == Emote.Playful;
    }
}
