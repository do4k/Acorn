using System.Collections.Concurrent;
using Acorn.Database.Models;
using GameCharacter = Acorn.Game.Models.Character;
using Inventory = Acorn.Game.Models.Inventory;
using Bank = Acorn.Game.Models.Bank;

namespace Acorn.Game.Mappers;

/// <summary>
///     Default implementation for mapping between game and database character models.
/// </summary>
public class CharacterMapper : ICharacterMapper
{
    public Character ToDatabase(GameCharacter character)
    {
        return new Character
        {
            Accounts_Username = character.Accounts_Username,
            Name = character.Name,
            Title = character.Title,
            Home = character.Home,
            Fiance = character.Fiance,
            Partner = character.Partner,
            Admin = character.Admin,
            Class = character.Class,
            Gender = character.Gender,
            Race = character.Race,
            HairStyle = character.HairStyle,
            HairColor = character.HairColor,
            Map = character.Map,
            X = character.X,
            Y = character.Y,
            Direction = character.Direction,
            Level = character.Level,
            Exp = character.Exp,
            MaxHp = character.MaxHp,
            Hp = character.Hp,
            MaxTp = character.MaxTp,
            Tp = character.Tp,
            MaxSp = character.MaxSp,
            Sp = character.Sp,
            Str = character.Str,
            Wis = character.Wis,
            Int = character.Int,
            Agi = character.Agi,
            Con = character.Con,
            Cha = character.Cha,
            MinDamage = character.MinDamage,
            MaxDamage = character.MaxDamage,
            MaxWeight = character.MaxWeight,
            Accuracy = character.Accuracy,
            Evade = character.Evade,
            Armor = character.Armor,
            StatPoints = character.StatPoints,
            SkillPoints = character.SkillPoints,
            Karma = character.Karma,
            SitState = character.SitState,
            Hidden = character.Hidden,
            NoInteract = character.NoInteract,
            BankMax = character.BankMax,
            GoldBank = character.GoldBank,
            Usage = character.Usage,
            Items = character.Inventory.Items.Select(i => new CharacterItem
            {
                CharacterName = character.Name!,
                ItemId = i.Id,
                Amount = i.Amount,
                Slot = 0, // Inventory
                Character = null // Explicitly set to null to avoid circular reference
            }).Concat(character.Bank.Items.Select(i => new CharacterItem
            {
                CharacterName = character.Name!,
                ItemId = i.Id,
                Amount = i.Amount,
                Slot = 1, // Bank
                Character = null // Explicitly set to null to avoid circular reference
            })).ToList(),
            Paperdoll = new CharacterPaperdoll
            {
                CharacterName = character.Name!,
                Hat = character.Paperdoll.Hat,
                Necklace = character.Paperdoll.Necklace,
                Armor = character.Paperdoll.Armor,
                Belt = character.Paperdoll.Belt,
                Boots = character.Paperdoll.Boots,
                Gloves = character.Paperdoll.Gloves,
                Weapon = character.Paperdoll.Weapon,
                Shield = character.Paperdoll.Shield,
                Accessory = character.Paperdoll.Accessory,
                Ring1 = character.Paperdoll.Ring1,
                Ring2 = character.Paperdoll.Ring2,
                Bracer1 = character.Paperdoll.Bracer1,
                Bracer2 = character.Paperdoll.Bracer2,
                Armlet1 = character.Paperdoll.Armlet1,
                Armlet2 = character.Paperdoll.Armlet2
            }
        };
    }

    public GameCharacter FromDatabase(Character dbCharacter)
    {
        var inventoryItems = dbCharacter.Items?
                                 .Where(i => i.Slot == 0)
                                 .Select(i => new ItemWithAmount { Id = i.ItemId, Amount = i.Amount })
                             ?? Enumerable.Empty<ItemWithAmount>();

        var bankItems = dbCharacter.Items?
                            .Where(i => i.Slot == 1)
                            .Select(i => new ItemWithAmount { Id = i.ItemId, Amount = i.Amount })
                        ?? Enumerable.Empty<ItemWithAmount>();

        return new GameCharacter
        {
            Accounts_Username = dbCharacter.Accounts_Username,
            Name = dbCharacter.Name,
            Title = dbCharacter.Title,
            Home = dbCharacter.Home,
            Fiance = dbCharacter.Fiance,
            Partner = dbCharacter.Partner,
            Admin = dbCharacter.Admin,
            Class = dbCharacter.Class,
            Gender = dbCharacter.Gender,
            Race = dbCharacter.Race,
            HairStyle = dbCharacter.HairStyle,
            HairColor = dbCharacter.HairColor,
            Map = dbCharacter.Map,
            X = dbCharacter.X,
            Y = dbCharacter.Y,
            Direction = dbCharacter.Direction,
            Level = dbCharacter.Level,
            Exp = dbCharacter.Exp,
            MaxHp = dbCharacter.MaxHp,
            Hp = dbCharacter.Hp,
            MaxTp = dbCharacter.MaxTp,
            Tp = dbCharacter.Tp,
            MaxSp = dbCharacter.MaxSp,
            Sp = dbCharacter.Sp,
            Str = dbCharacter.Str,
            Wis = dbCharacter.Wis,
            Int = dbCharacter.Int,
            Agi = dbCharacter.Agi,
            Con = dbCharacter.Con,
            Cha = dbCharacter.Cha,
            MinDamage = dbCharacter.MinDamage,
            MaxDamage = dbCharacter.MaxDamage,
            MaxWeight = dbCharacter.MaxWeight,
            Accuracy = dbCharacter.Accuracy,
            Evade = dbCharacter.Evade,
            Armor = dbCharacter.Armor,
            StatPoints = dbCharacter.StatPoints,
            SkillPoints = dbCharacter.SkillPoints,
            Karma = dbCharacter.Karma,
            SitState = dbCharacter.SitState,
            Hidden = dbCharacter.Hidden,
            NoInteract = dbCharacter.NoInteract,
            BankMax = dbCharacter.BankMax,
            GoldBank = dbCharacter.GoldBank,
            Usage = dbCharacter.Usage,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>(inventoryItems)),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>(bankItems)),
            Paperdoll = new Paperdoll
            {
                Hat = dbCharacter.Paperdoll?.Hat ?? 0,
                Necklace = dbCharacter.Paperdoll?.Necklace ?? 0,
                Armor = dbCharacter.Paperdoll?.Armor ?? 0,
                Belt = dbCharacter.Paperdoll?.Belt ?? 0,
                Boots = dbCharacter.Paperdoll?.Boots ?? 0,
                Gloves = dbCharacter.Paperdoll?.Gloves ?? 0,
                Weapon = dbCharacter.Paperdoll?.Weapon ?? 0,
                Shield = dbCharacter.Paperdoll?.Shield ?? 0,
                Accessory = dbCharacter.Paperdoll?.Accessory ?? 0,
                Ring1 = dbCharacter.Paperdoll?.Ring1 ?? 0,
                Ring2 = dbCharacter.Paperdoll?.Ring2 ?? 0,
                Bracer1 = dbCharacter.Paperdoll?.Bracer1 ?? 0,
                Bracer2 = dbCharacter.Paperdoll?.Bracer2 ?? 0,
                Armlet1 = dbCharacter.Paperdoll?.Armlet1 ?? 0,
                Armlet2 = dbCharacter.Paperdoll?.Armlet2 ?? 0
            }
        };
    }
}