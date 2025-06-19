using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Extensions;

public static class CoordsExtensions
{
    public static Coords NextCoords(this Coords coords, Direction direction)
    {
        var nextX = direction switch
        {
            Direction.Left => coords.X - 1,
            Direction.Right => coords.X + 1,
            _ => coords.X
        };
        var nextY = direction switch
        {
            Direction.Up => coords.Y - 1,
            Direction.Down => coords.Y + 1,
            _ => coords.Y
        };
        return new Coords
        {
            X = nextX,
            Y = nextY
        };
    }
}