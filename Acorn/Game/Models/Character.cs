using System.Collections.Concurrent;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Models;

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
    public required Inventory Inventory { get; set; }
    public required Bank Bank { get; set; }
    public required Paperdoll Paperdoll { get; set; }

    //TODO: Add spells
    //TODO: Add guilds
    //TODO: Add quests

    public Database.Models.Character AsDatabaseModel()
        => new()
        {
            Accounts_Username = Accounts_Username,
            Name = Name,
            Title = Title,
            Home = Home,
            Fiance = Fiance,
            Partner = Partner,
            Admin = Admin,
            Class = Class,
            Gender = Gender,
            Race = Race,
            HairStyle = HairStyle,
            HairColor = HairColor,
            Map = Map,
            X = X,
            Y = Y,
            Direction = Direction,
            Level = Level,
            Exp = Exp,
            MaxHp = MaxHp,
            Hp = Hp,
            MaxTp = MaxTp,
            Tp = Tp,
            MaxSp = MaxSp,
            Sp = Sp,
            Str = Str,
            Wis = Wis,
            Agi = Agi,
            Con = Con,
            Cha = Cha,
            MinDamage = MinDamage,
            MaxDamage = MaxDamage,
            MaxWeight = MaxWeight,
            StatPoints = StatPoints,
            SkillPoints = SkillPoints,
            Karma = Karma,
            SitState = SitState,
            Hidden = Hidden,
            NoInteract = NoInteract,
            BankMax = BankMax,
            GoldBank = GoldBank,
            Usage = Usage,
            Inventory = string.Join("|", Inventory.Items.Select(i => $"{i.Id},{i.Amount}")),
            Bank = string.Join("|", Bank.Items.Select(i => $"{i.Id},{i.Amount}")),
            Paperdoll = string.Join(",", new[]
            {
                Paperdoll.Hat, Paperdoll.Armor, Paperdoll.Shield, Paperdoll.Weapon, Paperdoll.Boots,
                Paperdoll.Gloves, Paperdoll.Necklace, Paperdoll.Belt, Paperdoll.Accessory,
                Paperdoll.Ring1, Paperdoll.Ring2, Paperdoll.Bracer1, Paperdoll.Bracer2,
                Paperdoll.Armlet1, Paperdoll.Armlet2
            })
        };

    public void CalculateStats(Ecf classes)
    {
        var @class = classes.GetClass(Class);
        if (@class is not null)
        {
            MaxHp = 100;
            MaxTp = 100;
            MaxSp = 100;
            MinDamage = 100;
            MaxDamage = 150;
            MaxWeight = 100;

            Hp = Hp > MaxHp ? MaxHp : Hp;
            Str = @class.Str + Str;
            Wis = @class.Wis + Wis;
            Agi = @class.Agi + Agi;
            Con = @class.Con + Con;
            Cha = @class.Cha + Cha;
        }
    }

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

    public int Recover(int amount)
    {
        Hp = Hp switch
        {
            var hp when hp < MaxHp && hp + amount < MaxHp => hp + amount,
            _ => MaxHp
        };

        return Hp;
    }

    public Coords NextCoords() => AsCoords().NextCoords(Direction);
}

public record Bank(ConcurrentBag<ItemWithAmount> Items);

public record Inventory(ConcurrentBag<ItemWithAmount> Items);