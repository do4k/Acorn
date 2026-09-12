namespace Acorn.World.Services.Spell;

/// <summary>
///     Who a Spell_Target* packet is aimed at. For OtherPlayer, VictimId is a session ID;
///     for Npc, VictimId is the NPC's map index.
/// </summary>
public readonly record struct SpellCastTarget(SpellCastTargetType Type, int VictimId = 0)
{
    public static SpellCastTarget Self()
    {
        return new SpellCastTarget(SpellCastTargetType.Self);
    }

    public static SpellCastTarget Group()
    {
        return new SpellCastTarget(SpellCastTargetType.Group);
    }

    public static SpellCastTarget Player(int sessionId)
    {
        return new SpellCastTarget(SpellCastTargetType.OtherPlayer, sessionId);
    }

    public static SpellCastTarget Npc(int npcIndex)
    {
        return new SpellCastTarget(SpellCastTargetType.Npc, npcIndex);
    }
}
