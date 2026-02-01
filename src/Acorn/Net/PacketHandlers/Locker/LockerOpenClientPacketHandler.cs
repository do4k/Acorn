using Acorn.Database.Repository;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Locker;

public class LockerOpenClientPacketHandler(
    ILogger<LockerOpenClientPacketHandler> logger,
    IMapTileService mapTileService)
    : IPacketHandler<LockerOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, LockerOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open locker without character or map", player.SessionId);
            return;
        }

        var playerCoords = new Coords { X = player.Character.X, Y = player.Character.Y };

        // Check if player is adjacent to a bank vault tile
        var adjacentCoords = new[]
        {
            new Coords { X = playerCoords.X, Y = playerCoords.Y - 1 },
            new Coords { X = playerCoords.X, Y = playerCoords.Y + 1 },
            new Coords { X = playerCoords.X - 1, Y = playerCoords.Y },
            new Coords { X = playerCoords.X + 1, Y = playerCoords.Y }
        };

        var hasAdjacentLocker = adjacentCoords.Any(coord =>
        {
            var tile = mapTileService.GetTile(player.CurrentMap.Data, coord);
            return tile == MapTileSpec.BankVault;
        });

        if (!hasAdjacentLocker)
        {
            logger.LogWarning("Player {Character} tried to open locker but is not adjacent to one",
                player.Character.Name);
            return;
        }

        logger.LogInformation("Player {Character} opening locker", player.Character.Name);

        // Build locker items list
        var lockerItems = player.Character.Bank.Items.Select(item => new ThreeItem
        {
            Id = item.Id,
            Amount = item.Amount
        }).ToList();

        await player.Send(new LockerOpenServerPacket
        {
            LockerCoords = playerCoords,
            LockerItems = lockerItems
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (LockerOpenClientPacket)packet);
    }
}
