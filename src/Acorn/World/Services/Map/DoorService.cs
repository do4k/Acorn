using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Models;
using Acorn.Net;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Services.Map;

/// <summary>
///     Door interaction logic, modelled on eoserv's <c>Map::OpenDoor</c>.
///     Doors are EMF warp entities whose <see cref="MapWarp.Door" /> field is non-zero
///     (see <see cref="MapLegacyDoorKey" /> for the legacy key list). The SDK has no
///     dedicated door tile spec, so the warp is authoritative rather than the tile spec.
/// </summary>
public class DoorService(
    IMapTileService tileService,
    IDataFileRepository dataFileRepository,
    ILogger<DoorService> logger)
    : IDoorService
{
    public DoorOpenResult ValidateDoorOpen(Character character, Coords coords, MapState map, out int requiredKey)
    {
        requiredKey = 0;

        if (coords.X < 0 || coords.Y < 0 || coords.X >= map.Data.Width || coords.Y >= map.Data.Height)
        {
            return DoorOpenResult.NotADoor;
        }

        var warp = FindWarp(map.Data, coords);
        if (warp is null || warp.Door <= 0)
        {
            return DoorOpenResult.NotADoor;
        }

        if (map.OpenedDoors.ContainsKey(coords))
        {
            return DoorOpenResult.AlreadyOpen;
        }

        if (!tileService.InClientRange(character.AsCoords(), coords))
        {
            return DoorOpenResult.OutOfRange;
        }

        // Door specs above Door (1) are locked: LockedSilver(2), LockedCrystal(3), LockedWraith(4).
        if (warp.Door > 1)
        {
            requiredKey = warp.Door;
            if (!HasKey(character, warp.Door))
            {
                return DoorOpenResult.Locked;
            }
        }

        return DoorOpenResult.Opened;
    }

    public async Task<bool> OpenDoorAsync(PlayerState player, Coords coords, MapState map)
    {
        if (player.Character is null)
        {
            return false;
        }

        var result = ValidateDoorOpen(player.Character, coords, map, out var requiredKey);

        if (result == DoorOpenResult.Locked)
        {
            logger.LogDebug("Player {Character} tried to open a locked door at ({X}, {Y})",
                player.Character.Name, coords.X, coords.Y);

            // Door/Close is the locked-door reply and carries the required key spec.
            await player.Send(new DoorCloseServerPacket { Key = requiredKey });
            return false;
        }

        if (result != DoorOpenResult.Opened)
        {
            logger.LogWarning("Player {Character} failed to open door at ({X}, {Y}): {Result}",
                player.Character.Name, coords.X, coords.Y, result);
            return false;
        }

        map.RegisterOpenedDoor(coords);

        // eoserv broadcasts Door/Open to every character in range, including the opener.
        var packet = new DoorOpenServerPacket { Coords = coords };
        var recipients = map.Players.Values
            .Where(p => p.Character is not null)
            .Where(p => tileService.InClientRange(coords, p.Character!.AsCoords()))
            .ToList();

        foreach (var recipient in recipients)
        {
            await recipient.Send(packet);
        }

        logger.LogInformation("Player {Character} opened door at ({X}, {Y})",
            player.Character.Name, coords.X, coords.Y);

        return true;
    }

    private bool HasKey(Character character, int keySpec)
    {
        return character.Inventory.Items.Any(item =>
        {
            var itemData = dataFileRepository.Eif.GetItem(item.Id);
            return itemData is { Type: ItemType.Key } && itemData.Spec1 == keySpec;
        });
    }

    private static MapWarp? FindWarp(Emf map, Coords coords)
    {
        var row = map.WarpRows.FirstOrDefault(r => r.Y == coords.Y);
        var tile = row?.Tiles.FirstOrDefault(t => t.X == coords.X);
        return tile?.Warp;
    }
}
