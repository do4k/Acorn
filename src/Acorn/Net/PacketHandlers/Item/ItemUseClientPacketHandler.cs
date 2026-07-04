using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Shared.Caching;
using Acorn.World;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Item;

[RequiresCharacter]
public class ItemUseClientPacketHandler(
    ILogger<ItemUseClientPacketHandler> logger,
    IWorldQueries worldQueries,
    IInventoryService inventoryService,
    IWeightCalculator weightCalculator,
    IFormulaService formulaService,
    IPlayerController playerController,
    IInnDataRepository innDataRepository,
    ICharacterCacheService characterCache,
    IPaperdollService paperdollService,
    ICharacterMapper characterMapper,
    IDbRepository<Database.Models.Character> characterRepository)
    : IPacketHandler<ItemUseClientPacket>
{
    public async Task HandleAsync(PlayerState player, ItemUseClientPacket packet)
    {
        // Validate player has the item
        if (!inventoryService.HasItem(player.Character!, packet.ItemId))
        {
            logger.LogWarning("Player {Character} tried to use item {ItemId} but doesn't have it",
                player.Character!.Name, packet.ItemId);
            return;
        }

        // Get item data
        var itemData = worldQueries.DataRepository.Eif.GetItem(packet.ItemId);
        if (itemData == null)
        {
            logger.LogError("Item {ItemId} not found in EIF", packet.ItemId);
            return;
        }

        logger.LogInformation("Player {Character} using item {ItemId} ({ItemName}) type {Type}",
            player.Character!.Name, packet.ItemId, itemData.Name, itemData.Type);

        var consumed = true;
        ItemReplyServerPacket.IItemTypeData? itemTypeData = null;

        switch (itemData.Type)
        {
            case ItemType.Heal:
                itemTypeData = await HandleHealItem(player, itemData);
                break;

            case ItemType.Teleport:
                consumed = await HandleTeleportItem(player, itemData);
                break;

            case ItemType.HairDye:
                itemTypeData = await HandleHairDye(player, itemData);
                break;

            case ItemType.ExpReward:
                itemTypeData = await HandleExpReward(player, itemData);
                break;

            default:
                logger.LogWarning("Item type {Type} not yet implemented", itemData.Type);
                consumed = false;
                break;
        }

        // Remove item from inventory if consumed
        if (consumed)
        {
            inventoryService.TryRemoveItem(player.Character!, packet.ItemId);

            var remainingAmount = inventoryService.GetItemAmount(player.Character!, packet.ItemId);
            var currentWeight = weightCalculator.GetCurrentWeight(player.Character!, worldQueries.DataRepository.Eif);

            await player.Send(new ItemReplyServerPacket
            {
                ItemType = itemData.Type,
                UsedItem = new Moffat.EndlessOnline.SDK.Protocol.Net.Item
                {
                    Id = packet.ItemId,
                    Amount = remainingAmount
                },
                Weight = new Weight
                {
                    Current = currentWeight,
                    Max = player.Character!.MaxWeight
                },
                ItemTypeData = itemTypeData
            });

            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character!));
        }
    }


    private async Task<ItemReplyServerPacket.IItemTypeData?> HandleHealItem(PlayerState player, EifRecord item)
    {
        if (player.Character == null)
        {
            return null;
        }

        var hpBefore = player.Character!.Hp;
        var tpBefore = player.Character!.Tp;

        // Heal HP
        if (item.Hp > 0)
        {
            player.Character!.Hp = Math.Min(player.Character!.Hp + item.Hp, player.Character!.MaxHp);
        }

        // Heal TP
        if (item.Tp > 0)
        {
            player.Character!.Tp = Math.Min(player.Character!.Tp + item.Tp, player.Character!.MaxTp);
        }

        var hpGain = player.Character!.Hp - hpBefore;
        var tpGain = player.Character!.Tp - tpBefore;

        logger.LogInformation("Player {Character} healed {HpGain} HP and {TpGain} TP",
            player.Character!.Name, hpGain, tpGain);

        if (hpGain > 0 && player.CurrentMap != null)
        {
            var hpPercentage = (int)Math.Round(player.Character!.Hp * 100.0 / player.Character!.MaxHp);

            await player.CurrentMap.BroadcastPacket(new RecoverAgreeServerPacket
            {
                PlayerId = player.SessionId,
                HealHp = hpGain,
                HpPercentage = hpPercentage
            }, player);
        }

        return new ItemReplyServerPacket.ItemTypeDataHeal
        {
            HpGain = hpGain,
            Hp = player.Character!.Hp,
            Tp = player.Character!.Tp
        };
    }

    private async Task<bool> HandleTeleportItem(PlayerState player, EifRecord item)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            return false;
        }

        int targetMapId;
        int targetX, targetY;

        if (item.Spec1 == 0)
        {
            // Teleport home
            var homeName = player.Character!.Home ?? innDataRepository.DefaultHomeName;
            var inn = innDataRepository.GetInnByName(homeName);
            if (inn == null)
            {
                logger.LogWarning("Player {Character} tried to scroll home but home inn '{Home}' was not found",
                    player.Character!.Name, homeName);
                return false;
            }

            targetMapId = inn.SpawnMap;
            targetX = inn.SpawnX;
            targetY = inn.SpawnY;
            logger.LogInformation("Player {Character} using scroll to teleport home ({Home})",
                player.Character!.Name, homeName);
        }
        else
        {
            // Teleport to specific map/coordinates
            targetMapId = item.Spec1;
            targetX = item.Spec2;
            targetY = item.Spec3;
            logger.LogInformation("Player {Character} using scroll to teleport to map {MapId} ({X}, {Y})",
                player.Character!.Name, targetMapId, targetX, targetY);
        }

        var targetMap = worldQueries.FindMap(targetMapId);
        if (targetMap == null)
        {
            logger.LogError("Player {Character} tried to scroll to unknown map {MapId}",
                player.Character!.Name, targetMapId);
            return false;
        }

        await playerController.WarpAsync(player, targetMap, targetX, targetY, WarpEffect.Scroll);

        return true;
    }

    private async Task<ItemReplyServerPacket.IItemTypeData?> HandleHairDye(PlayerState player, EifRecord item)
    {
        if (player.Character == null)
        {
            return null;
        }

        player.Character!.HairColor = item.Spec1;

        logger.LogInformation("Player {Character} changed hair color to {Color}",
            player.Character!.Name, item.Spec1);

        if (player.CurrentMap != null)
        {
            var avatarChange = new AvatarChange
            {
                PlayerId = player.SessionId,
                ChangeType = AvatarChangeType.HairColor,
                ChangeTypeData = new AvatarChange.ChangeTypeDataHairColor
                {
                    HairColor = item.Spec1
                }
            };

            await player.CurrentMap.BroadcastPacket(new AvatarAgreeServerPacket { Change = avatarChange }, player);
        }

        return new ItemReplyServerPacket.ItemTypeDataHairDye
        {
            HairColor = item.Spec1
        };
    }

    private async Task<ItemReplyServerPacket.IItemTypeData?> HandleExpReward(PlayerState player, EifRecord item)
    {
        if (player.Character == null)
        {
            return null;
        }

        var expGain = item.Spec1;
        player.Character!.GainExperience(expGain);

        logger.LogInformation("Player {Character} gained {Exp} experience from item",
            player.Character!.Name, expGain);

        var levelsGained = 0;
        while (formulaService.CanLevelUp(player.Character!))
        {
            formulaService.LevelUp(player.Character!, worldQueries.DataRepository.Ecf);
            levelsGained++;
        }

        if (levelsGained > 0)
        {
            await player.CacheCharacterStateAsync(characterCache, paperdollService);
        }

        return new ItemReplyServerPacket.ItemTypeDataExpReward
        {
            Experience = player.Character!.Exp,
            LevelUp = levelsGained > 0 ? player.Character!.Level : 0,
            StatPoints = player.Character!.StatPoints,
            SkillPoints = player.Character!.SkillPoints,
            MaxHp = player.Character!.MaxHp,
            MaxTp = player.Character!.MaxTp,
            MaxSp = player.Character!.MaxSp
        };
    }
}
