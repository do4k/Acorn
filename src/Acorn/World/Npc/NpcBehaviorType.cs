namespace Acorn.World.Npc;

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