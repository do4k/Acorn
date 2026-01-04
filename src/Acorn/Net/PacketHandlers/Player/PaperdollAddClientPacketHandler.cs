using Acorn.Extensions;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.World.Services.Player;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Microsoft.Extensions.Logging;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollAddClientPacketHandler : IPacketHandler<PaperdollAddClientPacket>
{
    private readonly IPlayerController _playerController;
    private readonly IInventoryService _inventoryService;
    private readonly IPaperdollService _paperdollService;
    private readonly ILogger<PaperdollAddClientPacketHandler> _logger;

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
        _logger.LogDebug("PaperdollAdd received - ItemId: {ItemId}, SubLoc: {SubLoc}, Player: {Player}", 
            packet.ItemId, packet.SubLoc, playerState.Account?.Username);
        
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

        // Send success response to player
        _logger.LogDebug("Sending PaperdollAgreeServerPacket for item {ItemId}, remaining: {Remaining}", 
            packet.ItemId, remainingAmount);
        await playerState.Send(new PaperdollAgreeServerPacket
        {
            ItemId = packet.ItemId,
            RemainingAmount = remainingAmount,
            SubLoc = packet.SubLoc
        });

        // Broadcast avatar change to nearby players
        var nearbyPlayers = playerState.CurrentMap.Players
            .Where(p => p.SessionId != playerState.SessionId)
            .ToList();

        _logger.LogDebug("Broadcasting equipment change to {NearbyPlayerCount} nearby players", nearbyPlayers.Count);

        if (nearbyPlayers.Count > 0)
        {
            var avatarChangePacket = new AvatarAgreeServerPacket
            {
                Change = new AvatarChange
                {
                    PlayerId = playerState.SessionId,
                    ChangeType = AvatarChangeType.Equipment,
                    ChangeTypeData = new AvatarChange.ChangeTypeDataEquipment
                    {
                        Equipment = character.Equipment().AsEquipmentChange(_paperdollService)
                    }
                }
            };
            
            var broadcastTasks = nearbyPlayers.Select(p => p.Send(avatarChangePacket)).ToList();
            await Task.WhenAll(broadcastTasks);
            _logger.LogDebug("Equipment broadcast complete");
        }
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
        => HandleAsync(playerState, (PaperdollAddClientPacket)packet);
}

