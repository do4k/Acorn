using System.Collections.Concurrent;
using Acorn.Database.Models;
using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Game.Models;

/// <summary>
/// Represents an in-game character. This is a pure data model.
/// For operations like inventory management, use IInventoryService.
/// For stat calculations, use IStatCalculator.
/// For database mapping, use ICharacterMapper.
/// </summary>
public class Character
{
    public required string Accounts_Username { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Home { get; set; }
    public string? Fiance { get; set; }
    public string? Partner { get; set; }
    public AdminLevel Admin { get; set; }
    public int Class { get; set; }
    public Gender Gender { get; set; }
    public int Race { get; set; }
    public int HairStyle { get; set; }
    public int HairColor { get; set; }
    public int Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Direction { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int MaxHp { get; set; }
    public int Hp { get; set; }
    public int MaxTp { get; set; }
    public int Tp { get; set; }
    public int MaxSp { get; set; }
    public int Sp { get; set; }
    public int Str { get; set; }
    public int Wis { get; set; }
    public int Agi { get; set; }
    public int Con { get; set; }
    public int Cha { get; set; }
    public int MinDamage { get; set; }
    public int MaxDamage { get; set; }
    public int MaxWeight { get; set; }
    public int Accuracy { get; set; }
    public int Evade { get; set; }
    public int Armor { get; set; }
    public int StatPoints { get; set; }
    public int SkillPoints { get; set; }
    public int Karma { get; set; }
    public SitState SitState { get; set; }
    public bool Hidden { get; set; }
    public bool NoInteract { get; set; }
    public int BankMax { get; set; }
    public int GoldBank { get; set; }
    public int Usage { get; set; }
    public required Inventory Inventory { get; set; }
    public required Bank Bank { get; set; }
    public required Paperdoll Paperdoll { get; set; }

    //TODO: Add spells
    //TODO: Add guilds
    //TODO: Add quests

    // Simple projection methods (no business logic)

    public IEnumerable<Item> Items()
    {
        return Inventory.Items.Select(x => new Item
        {
            Amount = x.Amount,
            Id = x.Id
        });
    }

    public Coords AsCoords()
    {
        return new Coords
        {
            X = X,
            Y = Y
        };
    }

    public EquipmentPaperdoll Equipment()
    {
        return new EquipmentPaperdoll
        {
            Hat = Paperdoll.Hat,
            Necklace = Paperdoll.Necklace,
            Armor = Paperdoll.Armor,
            Belt = Paperdoll.Belt,
            Boots = Paperdoll.Boots,
            Gloves = Paperdoll.Gloves,
            Weapon = Paperdoll.Weapon,
            Shield = Paperdoll.Shield,
            Accessory = Paperdoll.Accessory,
            Ring = [Paperdoll.Ring1, Paperdoll.Ring2],
            Bracer = [Paperdoll.Bracer1, Paperdoll.Bracer2],
            Armlet = [Paperdoll.Armlet1, Paperdoll.Armlet2]
        };
    }

    public Coords NextCoords() => AsCoords().NextCoords(Direction);

    /// <summary>
    /// Recover HP and TP based on divisor.
    /// Formula: (MaxHP/divisor + 1) for HP, (MaxTP/divisor + 1) for TP.
    /// Divisor should be 5 for standing, 10 for sitting.
    /// </summary>
    public (int Hp, int Tp) Recover(int divisor)
    {
        if (Hp < MaxHp)
        {
            var hpGain = (MaxHp / divisor) + 1;
            Hp = Math.Min(Hp + hpGain, MaxHp);
        }

        if (Tp < MaxTp)
        {
            var tpGain = (MaxTp / divisor) + 1;
            Tp = Math.Min(Tp + tpGain, MaxTp);
        }

        return (Hp, Tp);
    }

    /// <summary>
    /// Add experience to the character.
    /// </summary>
    public void GainExperience(int amount)
    {
        Exp += amount;
    }
}

public record Bank(ConcurrentBag<ItemWithAmount> Items);

public record Inventory(ConcurrentBag<ItemWithAmount> Items);