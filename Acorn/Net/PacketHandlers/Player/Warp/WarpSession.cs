using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

public class WarpSession
{
    public WarpEffect WarpEffect { get; }
    public int MapId { get; }
    public int X { get; }
    public int Y { get; }
    public ConnectionHandler ConnectionHandler { get; }
    public MapState TargetMap { get; }
    public bool IsLocal { get; }

    public WarpSession(int x, int y, ConnectionHandler connectionHandler, MapState targetMap, WarpEffect warpEffect = WarpEffect.None)
    {
        MapId = targetMap.Id;
        X = x;
        Y = y;
        WarpEffect = warpEffect;
        ConnectionHandler = connectionHandler;
        TargetMap = targetMap;
        IsLocal = connectionHandler.CurrentMap?.Id == MapId;
    }

    public async Task Execute()
    {
        if (IsLocal)
        {
            await ConnectionHandler.Send(new WarpRequestServerPacket
            {
                WarpType = WarpType.Local,
                MapId = MapId,
                SessionId = ConnectionHandler.SessionId,
                WarpTypeData = null
            });
            return;
        }

        await ConnectionHandler.Send(new WarpRequestServerPacket
        {
            WarpType = WarpType.MapSwitch,
            MapId = MapId,
            SessionId = ConnectionHandler.SessionId,
            WarpTypeData = new WarpRequestServerPacket.WarpTypeDataMapSwitch
            {
                MapFileSize = TargetMap.Data.ByteSize,
                MapRid = TargetMap.Data.Rid
            }
        });
    }
}