using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Net.PacketHandlers.Player.Warp;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Controllers;

public interface ICharacterController
{
    public Character Data { get; }
    Task Save();
    void SetStats(Ecf classes);
    void SetDirection(Direction direction);
    void GainExp(int exp);
    IEnumerable<Item> GetItems();
    Coords GetCoords();
    EquipmentPaperdoll GetEquipment();
    int Recover(int amount);
    Coords NextCoords();
    OnlinePlayer AsOnlinePlayer();
    CharacterMapInfo AsCharacterMapInfo(int playerId, WarpEffect wapEffect);
    CharacterStatsUpdate AsStatsUpdate();
    CharacterBaseStats AsBaseStats();
    CharacterSecondaryStats AsSecondaryStats();
    
}

public class CharacterController(
    Character character,
    IDbRepository<Character> characterRepository)
    : ICharacterController
{
    private readonly IDbRepository<Character> _characterRepository = characterRepository;

    public Character Data { get; } = character;
    public WarpSession? WarpSession { get; set; }

    public async Task Save()
    { 
        await _characterRepository.UpdateAsync(Data);
    }
    
    public void SetStats(Ecf classes)
    {
        var @class = classes.GetClass(Data.Class);
        if (@class is null)
        {
            return;
        }

        Data.MaxHp = 100;
        Data.MaxTp = 100;
        Data.MaxSp = 100;
        Data.MinDamage = 100;
        Data.MaxDamage = 150;
        Data.MaxWeight = 100;

        Data.Hp = Data.Hp >Data.MaxHp ? Data.MaxHp : Data.Hp;
        Data.Str = @class.Str + Data.Str;
        Data.Wis = @class.Wis + Data.Wis;
        Data.Agi = @class.Agi + Data.Agi;
        Data.Con = @class.Con + Data.Con;
        Data.Cha = @class.Cha + Data.Cha;
    }
    
    public void SetDirection(Direction direction)
    {
        Data.Direction = direction;
    }

    public void GainExp(int exp)
    {
        Data.Exp += exp;
    }

    public IEnumerable<Item> GetItems()
    {
        return Data.Inventory.Items.Select(x => new Item
        {
            Amount = x.Amount,
            Id = x.Id
        });
    }
    
    public Coords GetCoords()
    {
        return new Coords
        {
            X = Data.X,
            Y = Data.Y
        };
    }

    public EquipmentPaperdoll GetEquipment()
    {
        return new EquipmentPaperdoll
        {
            Hat = Data.Paperdoll.Hat,
            Necklace = Data.Paperdoll.Necklace,
            Armor = Data.Paperdoll.Armor,
            Belt = Data.Paperdoll.Belt,
            Boots = Data.Paperdoll.Boots,
            Gloves = Data.Paperdoll.Gloves,
            Weapon = Data.Paperdoll.Weapon,
            Shield = Data.Paperdoll.Shield,
            Accessory = Data.Paperdoll.Accessory,
            Ring = [Data.Paperdoll.Ring1, Data.Paperdoll.Ring2],
            Bracer = [Data.Paperdoll.Bracer1, Data.Paperdoll.Bracer2],
            Armlet = [Data.Paperdoll.Armlet1, Data.Paperdoll.Armlet2]
        };
    }

    public int Recover(int amount)
    {
        Data.Hp = Data.Hp switch
        {
            var hp when hp < Data.MaxHp && hp + amount < Data.MaxHp => hp + amount,
            _ => Data.MaxHp
        };

        return Data.Hp;
    }
    
    public OnlinePlayer AsOnlinePlayer()
    {
        return new OnlinePlayer
        {
            ClassId = Data.Class,
            GuildTag = "   ", //todo: guilds
            Icon = (int)Data.Admin switch
            {
                0 => CharacterIcon.Player,
                1 or 2 or 3 => CharacterIcon.Gm,
                _ => CharacterIcon.Hgm
                // todo: handle party
            },
            Level = Data.Level,
            Name = Data.Name,
            Title = Data.Title ?? ""
        };
    }

    public CharacterMapInfo AsCharacterMapInfo(int playerId, WarpEffect wapEffect)
    {
        return new CharacterMapInfo
        {
            ClassId = Data.Class,
            Direction = Data.Direction,
            Coords = new BigCoords { X = Data.X, Y = Data.Y },
            Equipment = new EquipmentMapInfo(),
            WarpEffect = wapEffect,
            Gender = Data.Gender,
            GuildTag = "   ", //todo: guilds
            HairColor = Data.HairColor,
            HairStyle = Data.HairStyle,
            Hp = Data.Hp,
            MaxHp = Data.MaxHp,
            MapId = Data.Map,
            MaxTp = Data.MaxTp,
            Name = Data.Name,
            Invisible = Data.Hidden,
            Level = Data.Level,
            PlayerId = playerId,
            SitState = Data.SitState,
            Tp = Data.Tp,
            Skin = Data.Race
        };
    }

    public CharacterStatsUpdate AsStatsUpdate()
    {
        return new CharacterStatsUpdate
        {
            MaxHp = Data.MaxHp,
            MaxTp = Data.MaxTp,
            MaxSp = Data.MaxSp,
            BaseStats = AsBaseStats(),
            SecondaryStats = AsSecondaryStats(),
            MaxWeight = Data.MaxWeight
        };
    }

    public CharacterBaseStats AsBaseStats()
    {
        return new CharacterBaseStats
        {
            Agi = Data.Agi,
            Cha = Data.Cha,
            Con = Data.Con,
            Str = Data.Str,
            Wis = Data.Wis
        };
    }

    public CharacterSecondaryStats AsSecondaryStats()
    {
        return new CharacterSecondaryStats
        {
            Accuracy = 0,
            Armor = 0,
            Evade = 0,
            MinDamage = Data.MinDamage,
            MaxDamage = Data.MaxDamage
        };
    }

    public Coords NextCoords() => GetCoords().NextCoords(Data.Direction);

    public async Task Warp(MapState targetMap, int x, int y, WarpEffect warpEffect = WarpEffect.None)
    {
        WarpSession = new WarpSession(x, y, targetMap, warpEffect);

        if (WarpSession.IsLocal is false)
        {
            if (CurrentMap is not null)
            {
                await CurrentMap.NotifyLeave(this, warpEffect);
            }

            await targetMap.NotifyEnter(this, warpEffect);
        }
        
        await WarpSession.Execute();
    }
}