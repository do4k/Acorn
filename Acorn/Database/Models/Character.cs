using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Database.Models;

public class Character
{
    public required string Accounts_Username { get; set; }
    
    [Key]
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
    
    // Navigation properties for relational data
    public ICollection<CharacterItem> Items { get; set; } = new List<CharacterItem>();
    public CharacterPaperdoll? Paperdoll { get; set; }

    public Game.Models.Character AsGameModel()
    {
        ConcurrentBag<ItemWithAmount> GetItemsFromDb(int slot)
        {
            var itemsList = Items
                .Where(i => i.Slot == slot)
                .Select(i => new ItemWithAmount { Id = i.ItemId, Amount = i.Amount })
                .ToList();
            
            return new ConcurrentBag<ItemWithAmount>(itemsList);
        }

        return new Game.Models.Character
        {
            Accounts_Username = Accounts_Username,
            Level = Level,
            Exp = Exp,
            Class = Class,
            Name = Name,
            Map = Map,
            X = X,
            Y = Y,
            Admin = Admin,
            HairColor = HairColor,
            HairStyle = HairStyle,
            Race = Race,
            Agi = Agi,
            Str = Str,
            Wis = Wis,
            Cha = Cha,
            Con = Con,
            Hp = Hp,
            MaxHp = MaxHp,
            Sp = Sp,
            MaxSp = MaxSp,
            Tp = Tp,
            MaxTp = MaxTp,
            Direction = Direction,
            Fiance = Fiance,
            Home = Home,
            Partner = Partner,
            SitState = SitState,
            MinDamage = MinDamage,
            MaxDamage = MaxDamage,
            MaxWeight = MaxWeight,
            Accuracy = Accuracy,
            Evade = Evade,
            Armor = Armor,
            StatPoints = StatPoints,
            SkillPoints = SkillPoints,
            Karma = Karma,
            Hidden = Hidden,
            NoInteract = NoInteract,
            Bank = new Bank(GetItemsFromDb(1)),
            Inventory = new Inventory(GetItemsFromDb(0)),
            Paperdoll = Paperdoll == null ? new Paperdoll() : new Paperdoll
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
                Ring1 = Paperdoll.Ring1,
                Ring2 = Paperdoll.Ring2,
                Bracer1 = Paperdoll.Bracer1,
                Bracer2 = Paperdoll.Bracer2,
                Armlet1 = Paperdoll.Armlet1,
                Armlet2 = Paperdoll.Armlet2
            },
            Usage = Usage,
        };
    }
}