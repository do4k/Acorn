namespace Acorn.World.Npc;

/// <summary>
///     NPC spawn type determines movement speed
///     Based on eoserv speed_table: 0.9, 0.6, 1.3, 1.9, 3.7, 7.5, 15.0, stationary
/// </summary>
public enum NpcSpawnType
{
    /// <summary>
    ///     Spawn type 0 - Speed 0.9 seconds between actions
    /// </summary>
    Type0 = 0,

    /// <summary>
    ///     Spawn type 1 - Speed 0.6 seconds (faster)
    /// </summary>
    Type1 = 1,

    /// <summary>
    ///     Spawn type 2 - Speed 1.3 seconds
    /// </summary>
    Type2 = 2,

    /// <summary>
    ///     Spawn type 3 - Speed 1.9 seconds
    /// </summary>
    Type3 = 3,

    /// <summary>
    ///     Spawn type 4 - Speed 3.7 seconds (slower)
    /// </summary>
    Type4 = 4,

    /// <summary>
    ///     Spawn type 5 - Speed 7.5 seconds (very slow)
    /// </summary>
    Type5 = 5,

    /// <summary>
    ///     Spawn type 6 - Speed 15.0 seconds (extremely slow)
    /// </summary>
    Type6 = 6,

    /// <summary>
    ///     Spawn type 7 - Stationary, never moves (direction set by spawn_time param)
    /// </summary>
    Stationary = 7
}

/// <summary>
///     NPC type from ENF file - determines functional behavior (shop, aggressive, etc.)
///     Based on EOSERV ENF::Type enum
/// </summary>
public enum NpcType
{
    /// <summary>
    ///     Regular NPC (type 0)
    /// </summary>
    Npc = 0,

    /// <summary>
    ///     Passive NPC - doesn't attack players (type 1)
    /// </summary>
    Passive = 1,

    /// <summary>
    ///     Aggressive NPC - attacks players on sight (type 2)
    /// </summary>
    Aggressive = 2,

    /// <summary>
    ///     Unknown type 3
    /// </summary>
    Unknown1 = 3,

    /// <summary>
    ///     Unknown type 4
    /// </summary>
    Unknown2 = 4,

    /// <summary>
    ///     Unknown type 5
    /// </summary>
    Unknown3 = 5,

    /// <summary>
    ///     Shop NPC (type 6)
    /// </summary>
    Shop = 6,

    /// <summary>
    ///     Inn/Citizenship NPC (type 7)
    /// </summary>
    Inn = 7,

    /// <summary>
    ///     Unknown type 8
    /// </summary>
    Unknown4 = 8,

    /// <summary>
    ///     Bank NPC (type 9)
    /// </summary>
    Bank = 9,

    /// <summary>
    ///     Barber NPC (type 10)
    /// </summary>
    Barber = 10,

    /// <summary>
    ///     Guild NPC (type 11)
    /// </summary>
    Guild = 11,

    /// <summary>
    ///     Priest NPC (type 12)
    /// </summary>
    Priest = 12,

    /// <summary>
    ///     Law NPC (type 13)
    /// </summary>
    Law = 13,

    /// <summary>
    ///     Skills/Skill Master NPC (type 14)
    /// </summary>
    Skills = 14,

    /// <summary>
    ///     Quest NPC (type 15)
    /// </summary>
    Quest = 15
}

/// <summary>
///     Custom movement behavior patterns for NPCs
///     Determines how the NPC moves around the map
/// </summary>
public enum NpcBehaviorType
{
    /// <summary>
    ///     NPC wanders randomly (default for aggressive/passive)
    /// </summary>
    Wander,

    /// <summary>
    ///     NPC doesn't move at all
    /// </summary>
    Stationary,

    /// <summary>
    ///     NPC patrols back and forth near spawn point
    /// </summary>
    Patrol,

    /// <summary>
    ///     NPC moves in a circular pattern
    /// </summary>
    Circle
}