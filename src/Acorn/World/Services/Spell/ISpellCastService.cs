using Acorn.Net;

namespace Acorn.World.Services.Spell;

/// <summary>
///     Handles spell chant timing, cast-time validation, and applying spell effects.
///     Mirrors the AttackUse melee flow but for ESF-backed spells.
/// </summary>
public interface ISpellCastService
{
    /// <summary>
    ///     Start a spell chant: validates the caster knows the spell and broadcasts
    ///     Spell_Request to nearby players.
    /// </summary>
    Task StartChantAsync(PlayerState player, int spellId);

    /// <summary>
    ///     Validate that the elapsed time between the chant start and the target packet
    ///     is consistent with the spell's cast time (reject casts that are too fast, and
    ///     stale casts that are too slow).
    /// </summary>
    bool ValidateCastTime(PlayerState player, int spellId, int timestamp);

    /// <summary>
    ///     Apply a spell's effect (heal or attack) against the given target.
    /// </summary>
    Task CastAsync(PlayerState player, int spellId, SpellCastTarget target);
}
