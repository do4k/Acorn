using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Map;

/// <summary>
///     Compares <see cref="Coords" /> by their X/Y position only.
/// </summary>
/// <remarks>
///     The SDK's generated <see cref="Coords.GetHashCode" /> also folds in
///     <c>ByteSize</c>, the number of bytes the instance was deserialized from.
///     Coordinates that arrive in a packet have a non-zero <c>ByteSize</c>, while
///     coordinates built in code have <c>ByteSize</c> 0. The two compare equal but
///     hash differently, so the default comparer silently fails dictionary lookups
///     (for example a door registered from a packet but looked up from the walk
///     handler). Use this comparer for any dictionary keyed by <see cref="Coords" />.
/// </remarks>
public sealed class CoordsComparer : IEqualityComparer<Coords>
{
    /// <summary>Shared stateless instance.</summary>
    public static readonly CoordsComparer Instance = new();

    private CoordsComparer()
    {
    }

    /// <inheritdoc />
    public bool Equals(Coords? x, Coords? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return x.X == y.X && x.Y == y.Y;
    }

    /// <inheritdoc />
    public int GetHashCode(Coords obj)
    {
        return HashCode.Combine(obj.X, obj.Y);
    }
}
