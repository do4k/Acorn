using Acorn.Net;
using Acorn.World.Npc;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services;

/// <summary>
/// Service for handling NPC combat logic.
/// </summary>
public interface INpcCombatService
{
    /// <summary>
    /// Register a player as an opponent when they attack an NPC.
    /// </summary>
    void AddOpponent(NpcState npc, int playerId, int damage);

    /// <summary>
    /// Increment bored ticks for all opponents and remove those who exceed threshold.
    /// </summary>
    void ProcessOpponentBoredom(NpcState npc, int incrementTicks, int boredThreshold);

    /// <summary>
    /// Try to have an NPC attack an adjacent player.
    /// Returns null if no valid attack target.
    /// </summary>
    NpcUpdateAttack? TryAttack(NpcState npc, int npcIndex, IEnumerable<PlayerState> players,
        IFormulaService formulaService);

    /// <summary>
    /// Get the act rate (ticks between actions) based on spawn type.
    /// </summary>
    int GetActRate(int spawnType);
}
