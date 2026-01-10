using Acorn.Domain.Models;
using Acorn.Game.Services;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Extensions;

public static class CharacterExtensions
{
    public static CharacterSelectionListEntry AsCharacterListEntry(this Character c, int id,
        IPaperdollService paperdollService)
    {
        return new CharacterSelectionListEntry
        {
            Admin = c.Admin,
            Equipment = paperdollService.ToEquipmentCharacterSelect(c),
            Gender = c.Gender,
            HairColor = c.HairColor,
            HairStyle = c.HairStyle,
            Level = c.Level,
            Name = c.Name,
            Skin = c.Race,
            Id = id
        };
    }

    public static CharacterMapInfo AsCharacterMapInfo(this Character c, int playerId, WarpEffect wapEffect,
        IPaperdollService paperdollService)
    {
        return new CharacterMapInfo
        {
            ClassId = c.Class,
            Direction = c.Direction,
            Coords = new BigCoords { X = c.X, Y = c.Y },
            Equipment = new EquipmentMapInfo
            {
                Armor = paperdollService.GetGraphicId(c.Paperdoll.Armor),
                Boots = paperdollService.GetGraphicId(c.Paperdoll.Boots),
                Weapon = paperdollService.GetGraphicId(c.Paperdoll.Weapon),
                Shield = paperdollService.GetGraphicId(c.Paperdoll.Shield),
                Hat = paperdollService.GetGraphicId(c.Paperdoll.Hat)
            },
            WarpEffect = wapEffect,
            Gender = c.Gender,
            GuildTag = "   ", //todo: guilds
            HairColor = c.HairColor,
            HairStyle = c.HairStyle,
            MapId = c.Map,
            Name = c.Name,
            Invisible = c.Hidden,
            Level = c.Level,
            PlayerId = playerId,
            SitState = c.SitState,
            Tp = c.Tp,
            MaxTp = c.MaxTp,
            Hp = c.Hp,
            MaxHp = c.MaxHp,
            Skin = c.Race
        };
    }

    public static OnlinePlayer AsOnlinePlayer(this Character c)
    {
        return new OnlinePlayer
        {
            ClassId = c.Class,
            GuildTag = "   ", //todo: guilds
            Icon = (int)c.Admin switch
            {
                0 => CharacterIcon.Player,
                1 or 2 or 3 => CharacterIcon.Gm,
                _ => CharacterIcon.Hgm
                // todo: handle party
            },
            Level = c.Level,
            Name = c.Name,
            Title = c.Title ?? ""
        };
    }

    public static CharacterStatsUpdate AsStatsUpdate(this Character c)
    {
        return new CharacterStatsUpdate
        {
            MaxHp = c.MaxHp,
            MaxTp = c.MaxTp,
            MaxSp = c.MaxSp,
            BaseStats = c.AsBaseStats(),
            SecondaryStats = c.AsSecondaryStats(),
            MaxWeight = c.MaxWeight
        };
    }

    public static CharacterBaseStats AsBaseStats(this Character c)
    {
        return new CharacterBaseStats
        {
            Agi = c.Agi,
            Cha = c.Cha,
            Con = c.Con,
            Str = c.Str,
            Wis = c.Wis,
            Intl = c.Int
        };
    }

    public static CharacterSecondaryStats AsSecondaryStats(this Character c)
    {
        return new CharacterSecondaryStats
        {
            Accuracy = c.Accuracy,
            Armor = c.Armor,
            Evade = c.Evade,
            MinDamage = c.MinDamage,
            MaxDamage = c.MaxDamage
        };
    }

    public static System.Collections.Generic.IEnumerable<ItemWithAmount> Items(this Character character)
    {
        return character.Inventory.Items;
    }

    public static Coords AsCoords(this Character character)
    {
        return new Coords
        {
            X = character.X,
            Y = character.Y
        };
    }

    public static EquipmentPaperdoll Equipment(this Character character)
    {
        return new EquipmentPaperdoll
        {
            Hat = character.Paperdoll.Hat,
            Necklace = character.Paperdoll.Necklace,
            Armor = character.Paperdoll.Armor,
            Belt = character.Paperdoll.Belt,
            Boots = character.Paperdoll.Boots,
            Gloves = character.Paperdoll.Gloves,
            Weapon = character.Paperdoll.Weapon,
            Shield = character.Paperdoll.Shield,
            Accessory = character.Paperdoll.Accessory,
            Ring = [character.Paperdoll.Ring1, character.Paperdoll.Ring2],
            Bracer = [character.Paperdoll.Bracer1, character.Paperdoll.Bracer2],
            Armlet = [character.Paperdoll.Armlet1, character.Paperdoll.Armlet2]
        };
    }

    public static Coords NextCoords(this Character character)
    {
        return character.AsCoords().NextCoords(character.Direction);
    }

    /// <summary>
    ///     Recover HP and TP based on divisor.
    ///     Formula: (MaxHP/divisor + 1) for HP, (MaxTP/divisor + 1) for TP.
    ///     Divisor should be 5 for standing, 10 for sitting.
    /// </summary>
    public static (int Hp, int Tp) Recover(this Character character, int divisor)
    {
        if (character.Hp < character.MaxHp)
        {
            var hpGain = character.MaxHp / divisor + 1;
            character.Hp = Math.Min(character.Hp + hpGain, character.MaxHp);
        }

        if (character.Tp < character.MaxTp)
        {
            var tpGain = character.MaxTp / divisor + 1;
            character.Tp = Math.Min(character.Tp + tpGain, character.MaxTp);
        }

        return (character.Hp, character.Tp);
    }

    /// <summary>
    ///     Add experience to the character.
    /// </summary>
    public static void GainExperience(this Character character, int amount)
    {
        character.Exp += amount;
    }

    /// <summary>
    ///     Get character stats for equipment change response.
    ///     Returns current stat values including equipment bonuses.
    /// </summary>
    public static CharacterStatsEquipmentChange GetCharacterStatsEquipmentChange(this Character character)
    {
        return new CharacterStatsEquipmentChange
        {
            MaxHp = character.MaxHp,
            MaxTp = character.MaxTp,
            BaseStats = new CharacterBaseStats
            {
                Str = character.Str,
                Intl = character.Int,
                Wis = character.Wis,
                Agi = character.Agi,
                Con = character.Con,
                Cha = character.Cha
            },
            SecondaryStats = new CharacterSecondaryStats
            {
                MinDamage = character.MinDamage,
                MaxDamage = character.MaxDamage,
                Accuracy = character.Accuracy,
                Evade = character.Evade,
                Armor = character.Armor
            }
        };
    }
}