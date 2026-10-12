using Acorn.Net;
using Acorn.World.Npc;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Npc;

/// <summary>
///     Service for handling NPC combat logic.
/// </summary>
public interface INpcCombatService
{
    /// <summary>
    ///     Register a player as an opponent when they attack an NPC.
    /// </summary>
    void AddOpponent(NpcState npc, int playerId, int damage);

    /// <summary>
    ///     Increment bored ticks for all opponents and remove those who exceed threshold.
    /// </summary>
    void ProcessOpponentBoredom(NpcState npc, int incrementTicks, int boredThreshold);

    /// <summary>
    ///     Try to have an NPC attack an adjacent player.
    ///     Returns null if no valid attack target.
    /// </summary>
    NpcUpdateAttack? TryAttack(NpcState npc, int npcIndex, IEnumerable<PlayerState> players,
        IFormulaService formulaService);

    /// <summary>
    ///     Get the base act interval in milliseconds for an NPC spawn type.
    ///     Returns 0 for immobile NPCs (spawn type 7).
    /// </summary>
    int GetActRateMs(int spawnType);

    /// <summary>
    ///     Roll the jittered delay until the NPC's next act (base rate +/-25%).
    /// </summary>
    double RollNextActDelayMs(int actRateMs);
}