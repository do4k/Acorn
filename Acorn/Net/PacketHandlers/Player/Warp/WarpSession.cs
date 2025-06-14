using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

public class WarpSession
{
    public WarpEffect WarpEffect { get; set; }
    public bool Local { get; set; }
    public int MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public async Task Begin(PlayerConnection player, WorldState worldState)
    {
        if (Local)
        {
            await player.Send(new WarpRequestServerPacket
            {
                WarpType = WarpType.Local,
                MapId = MapId,
                SessionId = player.SessionId,
                WarpTypeData = null
            });
            return;
        }

        var found = worldState.Maps.TryGetValue(MapId, out var map);
        if (found && map is not null)
        {
            await player.Send(new WarpRequestServerPacket
            {
                WarpType = WarpType.MapSwitch,
                MapId = MapId,
                SessionId = player.SessionId,
                WarpTypeData = new WarpRequestServerPacket.WarpTypeDataMapSwitch
                {
                    MapFileSize = map.Data.ByteSize,
                    MapRid = map.Data.Rid
                }
            });
        }
    }
}