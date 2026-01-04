using Acorn.Extensions;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollRemoveClientPacketHandler : IPacketHandler<PaperdollRemoveClientPacket>
{
    private readonly IPlayerController _playerController;
    private readonly IPaperdollService _paperdollService;
    private readonly ILogger<PaperdollRemoveClientPacketHandler> _logger;

    public PaperdollRemoveClientPacketHandler(
        IPlayerController playerController,
        IPaperdollService paperdollService,
        ILogger<PaperdollRemoveClientPacketHandler> logger)
    {
        _playerController = playerController;
        _paperdollService = paperdollService;
        _logger = logger;
    }
    public async Task HandleAsync(PlayerState playerState, PaperdollRemoveClientPacket packet)
    {
        if (playerState.Character is null || playerState.CurrentMap is null)
        {
            _logger.LogWarning("PaperdollRemove failed - Character is null: {CharIsNull}, CurrentMap is null: {MapIsNull}", 
                playerState.Character is null, playerState.CurrentMap is null);
            return;
        }

        var character = playerState.Character;

        // Use PlayerController to handle unequipping (includes stat recalculation)
        // SubLoc is only used for multi-slot items (Ring, Armlet, Bracer) as array index
        var unequipSuccess = await _playerController.UnequipItemAsync(playerState, packet.ItemId, packet.SubLoc);

        if (!unequipSuccess)
        {
            _logger.LogWarning("Player {Player} failed to unequip item {ItemId} from slot {SubLoc}", 
                character.Name, packet.ItemId, packet.SubLoc);
            return;
        }

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

        // Send remove response to player with stats and avatar change
        await playerState.Send(new PaperdollRemoveServerPacket
        {
            Change = avatarChange,
            ItemId = packet.ItemId,
            SubLoc = packet.SubLoc,
            Stats = character.GetCharacterStatsEquipmentChange()
        });

        // Broadcast avatar change to nearby players
        var broadcastPacket = new AvatarAgreeServerPacket { Change = avatarChange };
        await playerState.CurrentMap.BroadcastPacket(broadcastPacket, except: playerState);
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
        => HandleAsync(playerState, (PaperdollRemoveClientPacket)packet);
}

