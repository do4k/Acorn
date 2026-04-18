using Acorn.Database.Repository;
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
public class JukeboxOpenClientPacketHandler(
    ILogger<JukeboxOpenClientPacketHandler> logger,
    IMapTileService tileService)
    : IPacketHandler<JukeboxOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, JukeboxOpenClientPacket packet)
    {
        if (player.CurrentMap is null || player.Character is null)
        {
            return;
        }

        if (!tileService.PlayerInRangeOfTile(player.CurrentMap.Data, player.Character.AsCoords(), MapTileSpec.Jukebox))
        {
            logger.LogDebug("Player {Character} not in range of jukebox tile", player.Character.Name);
            return;
        }

        var jukeboxPlayer = player.CurrentMap.JukeboxTicks > 0
            ? player.CurrentMap.JukeboxPlayerName ?? "Busy"
            : string.Empty;

        await player.Send(new JukeboxOpenServerPacket
        {
            MapId = player.CurrentMap.Id,
            JukeboxPlayer = jukeboxPlayer
        });

        logger.LogInformation("Player {Character} opened jukebox on map {MapId}",
            player.Character.Name, player.CurrentMap.Id);
    }
}
