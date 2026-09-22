using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net.Services;
using Acorn.World;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $undress &lt;player&gt; - Force-unequips every item another player is wearing,
///     returning each item to their inventory. Cursed equipment resists removal,
///     mirroring the player-facing unequip rules.
/// </summary>
public class UndressCommandHandler(
    IWorldQueries world,
    IPlayerController playerController,
    IPaperdollService paperdollService,
    INotificationService notifications,
    ILogger<UndressCommandHandler> logger) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["undress"];

    public string Usage => "<player>";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $undress <player>");
            return;
        }

        var target = world.FindPlayerByName(args[0]);
        if (target?.Character is null || target.CurrentMap is null)
        {
            await notifications.SystemMessage(playerState, $"Player '{args[0]}' is not online.");
            return;
        }

        var character = target.Character;

        // Snapshot slot contents before mutating: multi-slot items repeat the
        // item id with a different array index (Ring/Armlet/Bracer pairs).
        var slots = new (int ItemId, int SubLoc)[]
        {
            (character.Paperdoll.Weapon, 0),
            (character.Paperdoll.Shield, 0),
            (character.Paperdoll.Armor, 0),
            (character.Paperdoll.Hat, 0),
            (character.Paperdoll.Boots, 0),
            (character.Paperdoll.Gloves, 0),
            (character.Paperdoll.Belt, 0),
            (character.Paperdoll.Necklace, 0),
            (character.Paperdoll.Accessory, 0),
            (character.Paperdoll.Ring1, 0),
            (character.Paperdoll.Ring2, 1),
            (character.Paperdoll.Armlet1, 0),
            (character.Paperdoll.Armlet2, 1),
            (character.Paperdoll.Bracer1, 0),
            (character.Paperdoll.Bracer2, 1)
        };

        var removed = 0;
        foreach (var (itemId, subLoc) in slots)
        {
            if (itemId == 0) continue;

            if (!await playerController.UnequipItemAsync(target, itemId, subLoc))
            {
                logger.LogWarning("Admin undress: {Admin} could not unequip item {ItemId} (slot index {SubLoc}) from {Target}",
                    playerState.Character?.Name, itemId, subLoc, character.Name);
                continue;
            }

            removed++;

            // Mirror PaperdollRemoveClientPacketHandler so the client UI stays in sync.
            var avatarChange = new AvatarChange
            {
                PlayerId = target.SessionId,
                ChangeType = AvatarChangeType.Equipment,
                ChangeTypeData = new AvatarChange.ChangeTypeDataEquipment
                {
                    Equipment = character.Equipment().AsEquipmentChange(paperdollService)
                }
            };

            await target.Send(new PaperdollRemoveServerPacket
            {
                Change = avatarChange,
                ItemId = itemId,
                SubLoc = subLoc,
                Stats = character.GetCharacterStatsEquipmentChange()
            });

            await target.CurrentMap.BroadcastPacket(new AvatarAgreeServerPacket { Change = avatarChange }, target);
        }

        if (removed > 0)
        {
            await notifications.ServerAnnouncement(target, "An administrator removed your equipment.");
        }

        await notifications.SystemMessage(playerState, removed > 0
            ? $"Removed {removed} item(s) from {character.Name}."
            : $"No equipment could be removed from {character.Name}.");
    }
}
