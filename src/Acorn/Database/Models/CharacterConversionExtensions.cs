using System.Collections.Concurrent;
using Acorn.Domain.Models;
using DatabaseCharacter = Acorn.Database.Models.Character;

namespace Acorn.Database.Models;

public static class CharacterConversionExtensions
{
    public static Domain.Models.Character AsGameModel(this DatabaseCharacter character)
    {
        ConcurrentBag<Domain.Models.ItemWithAmount> GetItemsFromDb(int slot)
        {
            var itemsList = character.Items
                .Where(i => i.Slot == slot)
                .Select(i => new Domain.Models.ItemWithAmount { Id = i.ItemId, Amount = i.Amount })
                .ToList();

            return new ConcurrentBag<Domain.Models.ItemWithAmount>(itemsList);
        }

        ConcurrentBag<Domain.Models.Spell> GetSpellsFromDb()
        {
            var spellsList = character.Spells
                .Select(s => new Domain.Models.Spell(s.SpellId, s.Level))
                .ToList();

            return new ConcurrentBag<Domain.Models.Spell>(spellsList);
        }

        return new Domain.Models.Character
        {
            Accounts_Username = character.Accounts_Username,
            Level = character.Level,
            Exp = character.Exp,
            Class = character.Class,
            Gender = character.Gender,
            Name = character.Name,
            Map = character.Map,
            X = character.X,
            Y = character.Y,
            Admin = character.Admin,
            HairColor = character.HairColor,
            HairStyle = character.HairStyle,
            Race = character.Race,
            Agi = character.Agi,
            Str = character.Str,
            Int = character.Int,
            Wis = character.Wis,
            Cha = character.Cha,
            Con = character.Con,
            Hp = character.Hp,
            MaxHp = character.MaxHp,
            Sp = character.Sp,
            MaxSp = character.MaxSp,
            Tp = character.Tp,
            MaxTp = character.MaxTp,
            Direction = character.Direction,
            Fiance = character.Fiance,
            Home = character.Home,
            Partner = character.Partner,
            SitState = character.SitState,
            MinDamage = character.MinDamage,
            MaxDamage = character.MaxDamage,
            MaxWeight = character.MaxWeight,
            Accuracy = character.Accuracy,
            Evade = character.Evade,
            Armor = character.Armor,
            StatPoints = character.StatPoints,
            SkillPoints = character.SkillPoints,
            Karma = character.Karma,
            Hidden = character.Hidden,
            NoInteract = character.NoInteract,
            Bank = new Bank(GetItemsFromDb(1)),
            Inventory = new Inventory(GetItemsFromDb(0)),
            Spells = new Domain.Models.Spells(GetSpellsFromDb()),
            Paperdoll = character.Paperdoll == null
                ? new Domain.Models.Paperdoll()
                : new Domain.Models.Paperdoll
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
                    Ring1 = character.Paperdoll.Ring1,
                    Ring2 = character.Paperdoll.Ring2,
                    Bracer1 = character.Paperdoll.Bracer1,
                    Bracer2 = character.Paperdoll.Bracer2,
                    Armlet1 = character.Paperdoll.Armlet1,
                    Armlet2 = character.Paperdoll.Armlet2
                },
            Usage = character.Usage
        };
    }
}
