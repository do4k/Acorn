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
            Accuracy = Accuracy,
            Evade = Evade,
            Armor = Armor,
            StatPoints = StatPoints,
            SkillPoints = SkillPoints,
            Karma = Karma,
            SitState = SitState,
            Hidden = Hidden,
            NoInteract = NoInteract,
            BankMax = BankMax,
            GoldBank = GoldBank,
            Usage = Usage,
            Items = Inventory.Items.Select(i => new Database.Models.CharacterItem
            {
                CharacterName = Name!,
                ItemId = i.Id,
                Amount = i.Amount,
                Slot = 0  // Inventory
            }).Concat(Bank.Items.Select(i => new Database.Models.CharacterItem
            {
                CharacterName = Name!,
                ItemId = i.Id,
                Amount = i.Amount,
                Slot = 1  // Bank
            })).ToList(),
            Paperdoll = new Database.Models.CharacterPaperdoll
            {
                CharacterName = Name!,
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
            }
        };

    public void CalculateStats(Ecf classes)
    {
        var @class = classes.GetClass(Class);
        if (@class is null)
        {
            return;
        }

        // Calculate adjusted stats from equipment bonuses
        // TODO: Add equipment stat bonuses when paperdoll items have stats
        int adj_str = Str;
        int adj_intl = Wis; // Note: SDK uses Wis for Int
        int adj_wis = Wis;
        int adj_agi = Agi;
        int adj_con = Con;
        int adj_cha = Cha;

        // Base MaxHP: (level * con / 20) + (level * class_con / 10) + base
        // EOSERV default: Level / 2 + 10 + (Con * 5) + (Class.Con * Level / 10)
        MaxHp = (Level / 2) + 10 + (adj_con * 5) + (@class.Con * Level / 10);
        MaxHp = Math.Min(MaxHp, 32767); // Short max value

        // Base MaxTP: (level * int / 20) + (level * class_int / 10) + base
        // EOSERV default: Level + (Wis * 2) + (Class.Wis * Level / 10)
        MaxTp = Level + (adj_intl * 2) + (@class.Wis * Level / 10);
        MaxTp = Math.Min(MaxTp, 32767);

        // Base MaxSP: (level * agi / 20) + (level * class_agi / 10) + base  
        // EOSERV default: Level / 4 + 50 + (Agi * 2) + (Class.Agi * Level / 10)
        MaxSp = (Level / 4) + 50 + (adj_agi * 2) + (@class.Agi * Level / 10);
        MaxSp = Math.Min(MaxSp, 32767);

        // Calculate MaxWeight
        // EOSERV: 70 + (Str * 5) + (Class.Str * Level / 10)
        MaxWeight = 70 + (adj_str * 5) + (@class.Str * Level / 10);
        MaxWeight = Math.Min(MaxWeight, 250);

        // Calculate base damage (before equipment)
        // EOSERV uses class-specific formulas based on StatGroup
        // StatGroup 1 (Warriors): Str-based damage
        // StatGroup 2 (Rogues): Agi-based damage
        // StatGroup 3 (Mages): Int-based damage  
        // StatGroup 4 (Archers): Agi-based damage
        // StatGroup 5 (Peasants): Balanced
        
        int baseDam = @class.StatGroup switch
        {
            1 => adj_str / 5,        // Warriors (Str)
            2 => adj_agi / 5,        // Rogues (Agi)
            3 => adj_intl / 5,       // Mages (Int)
            4 => adj_agi / 5,        // Archers (Agi)
            _ => (adj_str + adj_agi + adj_intl) / 15  // Balanced
        };

        MinDamage = 1 + baseDam;
        MaxDamage = 2 + baseDam + (Level / 10);

        // TODO: Add equipment damage bonuses

        MinDamage = Math.Min(MinDamage, 32767);
        MaxDamage = Math.Min(MaxDamage, 32767);

        // Calculate Accuracy (Agi-based + class bonus + level)
        Accuracy = (adj_agi / 2) + (@class.Agi / 4) + Level;
        // TODO: Add weapon accuracy bonus
        
        // Calculate Evade (Agi-based + class bonus)
        Evade = (adj_agi / 2) + (@class.Agi / 4);
        // TODO: Add armor evade bonus
        
        // Calculate Armor (Con-based)
        Armor = adj_con / 4;
        // TODO: Add equipment armor bonuses

        Accuracy = Math.Min(Accuracy, 32767);
        Evade = Math.Min(Evade, 32767);
        Armor = Math.Min(Armor, 32767);

        // Clamp HP/TP/SP to their new maximums
        Hp = Math.Min(Hp, MaxHp);
        Tp = Math.Min(Tp, MaxTp);
        Sp = Math.Min(Sp, MaxSp);
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

    /// <summary>
    /// Add experience to the character
    /// </summary>
    public void GainExperience(int amount)
    {
        Exp += amount;
    }

    public Coords NextCoords() => AsCoords().NextCoords(Direction);

    // Inventory Management Methods
    
    /// <summary>
    /// Adds an item to the player's inventory. Stacks with existing items if possible.
    /// </summary>
    /// <returns>True if item was added, false if inventory is full</returns>
    public bool AddItem(int itemId, int amount = 1)
    {
        if (amount <= 0) return false;

        // Try to find existing stack of this item
        var existingItem = Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (existingItem != null)
        {
            // Stack with existing item
            existingItem.Amount += amount;
            return true;
        }

        // Add new inventory slot
        // Note: No hard slot limit enforced here, limited by 2000 char serialization
        Inventory.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
        return true;
    }

    /// <summary>
    /// Removes an item from the player's inventory.
    /// </summary>
    /// <returns>True if item was removed, false if player doesn't have enough</returns>
    public bool RemoveItem(int itemId, int amount = 1)
    {
        if (amount <= 0) return false;

        var item = Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Amount < amount)
            return false;

        item.Amount -= amount;
        
        // Remove empty stacks
        if (item.Amount <= 0)
        {
            // ConcurrentBag doesn't have Remove, so we rebuild without this item
            var newItems = new ConcurrentBag<ItemWithAmount>(
                Inventory.Items.Where(i => i.Id != itemId || i.Amount > 0)
            );
            Inventory = new Inventory(newItems);
        }

        return true;
    }

    /// <summary>
    /// Checks if player has the specified item and amount.
    /// </summary>
    public bool HasItem(int itemId, int amount = 1)
    {
        var item = Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        return item != null && item.Amount >= amount;
    }

    /// <summary>
    /// Gets the total amount of a specific item in inventory.
    /// </summary>
    public int GetItemAmount(int itemId)
    {
        return Inventory.Items.FirstOrDefault(i => i.Id == itemId)?.Amount ?? 0;
    }

    /// <summary>
    /// Gets the number of used inventory slots.
    /// </summary>
    public int GetInventorySlotCount()
    {
        return Inventory.Items.Count;
    }

    // Bank Management Methods
    
    /// <summary>
    /// Adds an item to the player's bank. Stacks with existing items if possible.
    /// </summary>
    /// <returns>True if item was added, false if bank is full</returns>
    public bool AddBankItem(int itemId, int amount = 1)
    {
        if (amount <= 0) return false;

        // Check bank capacity
        if (Bank.Items.Count >= BankMax)
        {
            var existingItem = Bank.Items.FirstOrDefault(i => i.Id == itemId);
            if (existingItem == null)
                return false; // Bank full and no existing stack
                
            existingItem.Amount += amount;
            return true;
        }

        // Try to stack with existing item
        var item = Bank.Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            item.Amount += amount;
            return true;
        }

        // Add new bank slot
        Bank.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
        return true;
    }

    /// <summary>
    /// Removes an item from the player's bank.
    /// </summary>
    /// <returns>True if item was removed, false if player doesn't have enough</returns>
    public bool RemoveBankItem(int itemId, int amount = 1)
    {
        if (amount <= 0) return false;

        var item = Bank.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Amount < amount)
            return false;

        item.Amount -= amount;
        
        // Remove empty stacks
        if (item.Amount <= 0)
        {
            var newItems = new ConcurrentBag<ItemWithAmount>(
                Bank.Items.Where(i => i.Id != itemId || i.Amount > 0)
            );
            Bank = new Bank(newItems);
        }

        return true;
    }

    /// <summary>
    /// Checks if player has the specified item and amount in bank.
    /// </summary>
    public bool HasBankItem(int itemId, int amount = 1)
    {
        var item = Bank.Items.FirstOrDefault(i => i.Id == itemId);
        return item != null && item.Amount >= amount;
    }

    /// <summary>
    /// Calculates the current weight of items in inventory.
    /// </summary>
    public int GetCurrentWeight(Eif items)
    {
        int totalWeight = 0;
        foreach (var invItem in Inventory.Items)
        {
            var itemData = items.GetItem(invItem.Id);
            if (itemData != null)
            {
                totalWeight += itemData.Weight * invItem.Amount;
            }
        }
        return totalWeight;
    }

    /// <summary>
    /// Checks if adding an item would exceed weight limit.
    /// </summary>
    public bool CanCarryWeight(Eif items, int itemId, int amount = 1)
    {
        var itemData = items.GetItem(itemId);
        if (itemData == null) return false;

        int additionalWeight = itemData.Weight * amount;
        return GetCurrentWeight(items) + additionalWeight <= MaxWeight;
    }
}

public record Bank(ConcurrentBag<ItemWithAmount> Items);

public record Inventory(ConcurrentBag<ItemWithAmount> Items);