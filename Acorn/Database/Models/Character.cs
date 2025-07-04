using System.Collections.Concurrent;
using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Database.Models;

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
    public int StatPoints { get; set; }
    public int SkillPoints { get; set; }
    public int Karma { get; set; }
    public SitState SitState { get; set; }
    public bool Hidden { get; set; }
    public bool NoInteract { get; set; }
    public int BankMax { get; set; }
    public int GoldBank { get; set; }
    public int Usage { get; set; }
    public string? Inventory { get; set; }
    public string? Bank { get; set; }
    public string? Paperdoll { get; set; }

    public Game.Models.Character AsGameModel()
    {
        Paperdoll GetPaperdoll(Character c)
        {
            var items = c.Paperdoll.Split(',');
            return new Paperdoll
            {
                Hat = items.Length > 0 && int.TryParse(items[0], out var hatId) ? hatId : 0,
                Armor = items.Length > 1 && int.TryParse(items[1], out var armorId) ? armorId : 0,
                Shield = items.Length > 2 && int.TryParse(items[2], out var shieldId) ? shieldId : 0,
                Weapon = items.Length > 3 && int.TryParse(items[3], out var weaponId) ? weaponId : 0,
                Boots = items.Length > 4 && int.TryParse(items[4], out var bootsId) ? bootsId : 0,
                Gloves = items.Length > 5 && int.TryParse(items[5], out var glovesId) ? glovesId : 0,
                Necklace = items.Length > 6 && int.TryParse(items[6], out var necklaceId) ? necklaceId : 0,
                Belt = items.Length > 7 && int.TryParse(items[7], out var beltId) ? beltId : 0,
                Accessory = items.Length > 8 && int.TryParse(items[8], out var accessoryId) ? accessoryId : 0,
                Ring1 = items.Length > 9 && int.TryParse(items[9], out var ring1Id) ? ring1Id : 0,
                Ring2 = items.Length > 10 && int.TryParse(items[10], out var ring2Id) ? ring2Id : 0,
                Bracer1 = items.Length > 11 && int.TryParse(items[11], out var bracer1Id) ? bracer1Id : 0,
                Bracer2 = items.Length > 12 && int.TryParse(items[12], out var bracer2Id) ? bracer2Id : 0,
                Armlet1 = items.Length > 13 && int.TryParse(items[13], out var armlet1Id) ? armlet1Id : 0,
                Armlet2 = items.Length > 14 && int.TryParse(items[14], out var armlet2Id) ? armlet2Id : 0
            };
        }

        ConcurrentBag<ItemWithAmount> GetItemsWithAmount(string? s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return [];
            }

            return new ConcurrentBag<ItemWithAmount>(
                s.Split('|').Select(b =>
                {
                    var parts = b.Split(',');
                    return new ItemWithAmount
                    {
                        Id = int.Parse(parts[0]),
                        Amount = int.Parse(parts[1]),
                    };
                }).ToList());
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
            StatPoints = StatPoints,
            SkillPoints = SkillPoints,
            Karma = Karma,
            Hidden = Hidden,
            NoInteract = NoInteract,
            Bank = new Bank(GetItemsWithAmount(Bank)),
            Inventory = new Inventory(GetItemsWithAmount(Inventory)),
            Paperdoll = GetPaperdoll(this),
            Usage = Usage,
        };
    }
}