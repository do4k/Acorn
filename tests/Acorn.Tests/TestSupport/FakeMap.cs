using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Gemini;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Options;
using Acorn.World.Map;
using Acorn.World.Services;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using NSubstitute;

namespace Acorn.Tests.TestSupport;

/// <summary>
///     Builds a bare in-memory <see cref="MapState" /> (20x20, no NPCs) for testing
///     range-limited broadcasts.
/// </summary>
internal static class FakeMap
{
    public static MapState Create(int width = 20, int height = 20)
    {
        var emf = new Emf
        {
            Name = "TestMap",
            Width = width,
            Height = height,
            Npcs = new List<MapNpc>(),
            Items = new List<Moffat.EndlessOnline.SDK.Protocol.Map.MapItem>(),
            TileSpecRows = new List<MapTileSpecRow>(),
            WarpRows = new List<MapWarpRow>(),
            GraphicLayers = Enumerable.Range(0, 9).Select(_ => new MapGraphicLayer()).ToList(),
            Signs = new List<MapSign>(),
            LegacyDoorKeys = new List<MapLegacyDoorKey>(),
            Rid = new List<int> { 1, 2 }
        };

        return new MapState(
            new MapWithId(1, emf),
            Substitute.For<IDataFileRepository>(),
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            Substitute.For<INpcController>(),
            Substitute.For<IMapTileService>(),
            Substitute.For<IPaperdollService>(),
            90,
            false,
            30,
            NullLogger<MapState>.Instance);
    }
}
