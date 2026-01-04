using Acorn.Game.Models;
using Acorn.Game.Services;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Extensions;

public static class CharacterExtensions
{
    public static CharacterSelectionListEntry AsCharacterListEntry(this Character c, int id, IPaperdollService paperdollService)
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

    public static CharacterMapInfo AsCharacterMapInfo(this Character c, int playerId, WarpEffect wapEffect, IPaperdollService paperdollService)
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
            Wis = c.Wis
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
}