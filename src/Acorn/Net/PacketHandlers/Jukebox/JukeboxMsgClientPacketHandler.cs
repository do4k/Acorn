using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Options;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Jukebox;

[RequiresCharacter]
public class JukeboxMsgClientPacketHandler(
    ILogger<JukeboxMsgClientPacketHandler> logger,
    IMapTileService tileService,
    IInventoryService inventoryService,
    IOptions<JukeboxOptions> jukeboxOptions)
    : IPacketHandler<JukeboxMsgClientPacket>
{
    private const int GoldItemId = 1;

    public async Task HandleAsync(PlayerState player, JukeboxMsgClientPacket packet)
    {
        if (player.CurrentMap is null || player.Character is null)
        {
            return;
        }

        // Cannot play jukebox while trading
        if (player.TradeSession is not null)
        {
            return;
        }

        var options = jukeboxOptions.Value;

        if (!tileService.PlayerInRangeOfTile(player.CurrentMap.Data, player.Character.AsCoords(), MapTileSpec.Jukebox))
        {
            return;
        }

        // The client sends 0-based track IDs, server uses 1-based
        var trackId = packet.TrackId + 1;

        // Validate: jukebox already playing, not enough gold, invalid track
        if (player.CurrentMap.JukeboxTicks > 0
            || inventoryService.GetItemAmount(player.Character, GoldItemId) < options.Cost
            || trackId < 1
            || trackId > options.MaxTrackId)
        {
            await player.Send(new JukeboxReplyServerPacket());
            return;
        }

        // Deduct gold
        inventoryService.TryRemoveItem(player.Character, GoldItemId, options.Cost);

        // Set jukebox state on the map
        player.CurrentMap.JukeboxPlayerName = player.Character.Name;
        player.CurrentMap.JukeboxTicks = options.TrackTimer;

        // Send gold update to the player
        await player.Send(new JukeboxAgreeServerPacket
        {
            GoldAmount = inventoryService.GetItemAmount(player.Character, GoldItemId)
        });

        // Broadcast the track to all players on the map
        await player.CurrentMap.BroadcastPacket(new JukeboxUseServerPacket
        {
            TrackId = trackId
        });

        logger.LogInformation("Player {Character} played jukebox track {TrackId} on map {MapId}",
            player.Character.Name, trackId, player.CurrentMap.Id);
    }
}
