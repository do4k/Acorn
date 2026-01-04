using Acorn.Net;
using Acorn.World.Npc;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.World.Services.Npc;

/// <summary>
///     Result of an NPC movement attempt.
/// </summary>
public record NpcMoveResult(bool Moved, Direction Direction, Coords NewCoords);

/// <summary>
///     Service for controlling NPC behavior including movement, chase logic, and spawning.
///     Separates control logic from NPC state data.
/// </summary>
public interface INpcController
{
    /// <summary>
    ///     Attempt to move an NPC. Handles chase logic if the NPC has opponents or is aggressive.
    /// </summary>
    /// <param name="npc">The NPC to move</param>
    /// <param name="players">Players on the current map</param>
    /// <param name="npcs">All NPCs on the current map</param>
    /// <param name="mapData">The map data for tile/boundary checking</param>
    /// <returns>Move result indicating if movement occurred and the new position</returns>
    NpcMoveResult TryMove(NpcState npc, IEnumerable<PlayerState> players, IEnumerable<NpcState> npcs, Emf mapData);

    /// <summary>
    ///     Find a valid spawn position with variance for an NPC.
    /// </summary>
    /// <param name="npc">The NPC being spawned</param>
    /// <param name="baseX">Base spawn X coordinate</param>
    /// <param name="baseY">Base spawn Y coordinate</param>
    /// <param name="players">Players on the map (to avoid spawning on them)</param>
    /// <param name="npcs">NPCs on the map (to avoid spawning on them)</param>
    /// <param name="mapData">The map data for tile checking</param>
    /// <returns>Valid spawn coordinates</returns>
    (int X, int Y) FindSpawnPosition(NpcState npc, int baseX, int baseY,
        IEnumerable<PlayerState> players, IEnumerable<NpcState> npcs, Emf mapData);

    /// <summary>
    ///     Determine if spawn variance should be used for this NPC.
    /// </summary>
    bool ShouldUseSpawnVariance(NpcState npc);

    /// <summary>
    ///     Get a random direction for the NPC to spawn facing.
    /// </summary>
    Direction GetSpawnDirection(NpcState npc);

    /// <summary>
    ///     Determine behavior type for a newly created NPC.
    /// </summary>
    NpcBehaviorType DetermineBehaviorType(NpcState npc);
}