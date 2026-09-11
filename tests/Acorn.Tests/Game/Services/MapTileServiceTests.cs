using Acorn.World.Services.Map;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class MapTileServiceTests
{
    private readonly MapTileService _sut = new();

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 3, 4, 7)]
    [InlineData(5, 5, 5, 6, 1)]
    [InlineData(5, 5, 6, 6, 2)]
    [InlineData(10, 10, 7, 8, 5)]
    public void GetManhattanDistance_ShouldReturnSumOfAbsoluteDeltas(int ax, int ay, int bx, int by, int expected)
    {
        var a = new Coords { X = ax, Y = ay };
        var b = new Coords { X = bx, Y = by };

        _sut.GetManhattanDistance(a, b).Should().Be(expected);
    }

    [Fact]
    public void GetManhattanDistance_ShouldBeSymmetric()
    {
        var a = new Coords { X = 2, Y = 9 };
        var b = new Coords { X = 7, Y = 3 };

        _sut.GetManhattanDistance(a, b).Should().Be(_sut.GetManhattanDistance(b, a));
    }

    [Fact]
    public void GetDistance_ShouldRemainChebyshev()
    {
        var a = new Coords { X = 0, Y = 0 };
        var b = new Coords { X = 3, Y = 4 };

        _sut.GetDistance(a, b).Should().Be(4);
        _sut.GetManhattanDistance(a, b).Should().Be(7);
    }
}
