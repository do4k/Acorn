using Acorn.Extensions;
using Acorn.Game.Models;
using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.World.Services.Map;

/// <inheritdoc cref="IChestService" />
public class ChestService(IMapTileService tileService) : IChestService
{
    private const int InteractionDistance = 1;

    public bool IsInBounds(MapState map, Coords coords)
    {
        return coords.X >= 0 && coords.Y >= 0 && coords.X < map.Data.Width && coords.Y < map.Data.Height;
    }

    public bool IsChestTile(MapState map, Coords coords)
    {
        return tileService.GetTile(map.Data, coords) == MapTileSpec.Chest;
    }

    public bool IsAdjacent(Character character, Coords coords)
    {
        return tileService.GetManhattanDistance(character.AsCoords(), coords) <= InteractionDistance;
    }

    public MapChest GetOrCreateChest(MapState map, Coords coords)
    {
        return map.Chests.GetOrAdd(coords, _ => new MapChest { Coords = coords });
    }

    public IReadOnlyList<PlayerState> GetNearbyObservers(MapState map, Coords coords, PlayerState? except = null)
    {
        return map.Players.Values
            .Where(p => p.Character is not null && p.SessionId != except?.SessionId)
            .Where(p => tileService.GetManhattanDistance(p.Character!.AsCoords(), coords) <= InteractionDistance)
            .ToList();
    }

    public List<ThreeItem> ToThreeItems(MapChest chest)
    {
        return chest.Items.Select(item => new ThreeItem
        {
            Id = item.ItemId,
            Amount = item.Amount
        }).ToList();
    }
}
