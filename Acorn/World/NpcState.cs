using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World;


public class NpcState
{
    public NpcState(EnfRecord data)
    {
        Data = data;
    }

    public EnfRecord Data { get; set; }
    public Direction Direction { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Id { get; set; }
    public int Hp { get; set; }

    public Coords AsCoords()
    {
        return new Coords
        {
            X = X,
            Y = Y
        };
    }

    public Coords NextCoords()
    {
        return AsCoords().NextCoords(Direction);
    }

    public NpcMapInfo AsNpcMapInfo(int index)
    {
        return new NpcMapInfo
        {
            Coords = new Coords { X = X, Y = Y },
            Direction = Direction,
            Id = Id,
            Index = index
        };
    }
}