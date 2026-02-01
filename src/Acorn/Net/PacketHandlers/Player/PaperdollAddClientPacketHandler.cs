using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollAddClientPacketHandler : IPacketHandler<PaperdollAddClientPacket>
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<PaperdollAddClientPacketHandler> _logger;
    private readonly IPaperdollService _paperdollService;
    private readonly IPlayerController _playerController;

    public PaperdollAddClientPacketHandler(
        IPlayerController playerController,
        IInventoryService inventoryService,
        IPaperdollService paperdollService,
        ILogger<PaperdollAddClientPacketHandler> logger)
    {
        _playerController = playerController;
        _inventoryService = inventoryService;
        _paperdollService = paperdollService;
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState, PaperdollAddClientPacket packet)
    {
        if (playerState.Character is null || playerState.CurrentMap is null)
        {
            _logger.LogWarning("PaperdollAdd failed - Character is null: {CharIsNull}, CurrentMap is null: {MapIsNull}",
                playerState.Character is null, playerState.CurrentMap is null);
            return;
        }

        var character = playerState.Character;

        // Check if the player has the item
        var hasItem = _inventoryService.HasItem(character, packet.ItemId);
        _logger.LogDebug("Player {Player} has item {ItemId}: {HasItem}",
            character.Name, packet.ItemId, hasItem);

        if (!hasItem)
        {
            _logger.LogWarning("Player {Player} attempted to equip item {ItemId} which they don't have",
                character.Name, packet.ItemId);
            return;
        }

        // Use PlayerController to handle equipping (includes stat recalculation)
        // SubLoc is only used for multi-slot items (Ring, Armlet, Bracer) as array index
        var equipSuccess = await _playerController.EquipItemAsync(playerState, packet.ItemId, packet.SubLoc);

        if (!equipSuccess)
        {
            _logger.LogWarning("Player {Player} failed to equip item {ItemId} to slot {SubLoc}",
                character.Name, packet.ItemId, packet.SubLoc);
            return;
        }

        // Get remaining amount after equip
        var remainingAmount = _inventoryService.GetItemAmount(character, packet.ItemId);
        _logger.LogDebug("Remaining amount of item {ItemId}: {Amount}", packet.ItemId, remainingAmount);

        // Create avatar change for the response
        var avatarChange = new AvatarChange
        {
            PlayerId = playerState.SessionId,
            ChangeType = AvatarChangeType.Equipment,
            ChangeTypeData = new AvatarChange.ChangeTypeDataEquipment
            {
                Equipment = character.Equipment().AsEquipmentChange(_paperdollService)
            }
        };

        // Send success response to player with stats and avatar change
        _logger.LogDebug("Sending PaperdollAgreeServerPacket for item {ItemId}, remaining: {Remaining}",
            packet.ItemId, remainingAmount);
        await playerState.Send(new PaperdollAgreeServerPacket
        {
            Change = avatarChange,
            ItemId = packet.ItemId,
            RemainingAmount = remainingAmount,
            SubLoc = packet.SubLoc,
            Stats = character.GetCharacterStatsEquipmentChange()
        });

        // Broadcast avatar change to nearby players
        var broadcastPacket = new AvatarAgreeServerPacket { Change = avatarChange };
        await playerState.CurrentMap.BroadcastPacket(broadcastPacket, playerState);
    }

}