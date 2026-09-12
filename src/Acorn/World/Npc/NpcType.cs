namespace Acorn.World.Npc;

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
