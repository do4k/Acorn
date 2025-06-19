using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

public class WarpSession
{
    public WarpEffect WarpEffect { get; }
    public int MapId { get; }
    public int X { get; }
    public int Y { get; }
    public PlayerState Player { get; }
    public MapState TargetMap { get; }
    public bool IsLocal { get; }

    public WarpSession(int x, int y, PlayerState player, MapState targetMap, WarpEffect warpEffect = WarpEffect.None)
    {
        MapId = targetMap.Id;
        X = x;
        Y = y;
        WarpEffect = warpEffect;
        Player = player;
        TargetMap = targetMap;
        IsLocal = player.CurrentMap?.Id == MapId;
    }

    public async Task Execute()
    {
        if (IsLocal)
        {
            await Player.Send(new WarpRequestServerPacket
            {
                WarpType = WarpType.Local,
                MapId = MapId,
                SessionId = Player.SessionId,
                WarpTypeData = null
            });
            return;
        }

        await Player.Send(new WarpRequestServerPacket
        {
            WarpType = WarpType.MapSwitch,
            MapId = MapId,
            SessionId = Player.SessionId,
            WarpTypeData = new WarpRequestServerPacket.WarpTypeDataMapSwitch
            {
                MapFileSize = TargetMap.Data.ByteSize,
                MapRid = TargetMap.Data.Rid
            }
        });
    }
}