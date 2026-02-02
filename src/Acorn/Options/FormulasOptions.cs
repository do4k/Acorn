namespace Acorn.Options;

/// <summary>
///     Configuration for stat calculation formulas.
///     Based on reoserv's Formulas.ron config approach.
/// </summary>
public class FormulasOptions
{
    /// <summary>
    ///     Maximum HP formula: base + (hp_level_multiplier * level) + (hp_stat_multiplier * con)
    /// </summary>
    public int MaxHpBase { get; set; } = 10;
    public double MaxHpLevelMultiplier { get; set; } = 2.5;
    public double MaxHpStatMultiplier { get; set; } = 2.5;

    /// <summary>
    ///     Maximum TP formula: base + (tp_level_multiplier * level) + (tp_int_multiplier * int) + (tp_wis_multiplier * wis)
    /// </summary>
    public int MaxTpBase { get; set; } = 10;
    public double MaxTpLevelMultiplier { get; set; } = 2.5;
    public double MaxTpIntMultiplier { get; set; } = 2.5;
    public double MaxTpWisMultiplier { get; set; } = 1.5;

    /// <summary>
    ///     Maximum SP formula: base + (sp_level_multiplier * level)
    /// </summary>
    public int MaxSpBase { get; set; } = 20;
    public double MaxSpLevelMultiplier { get; set; } = 2.0;

    /// <summary>
    ///     Maximum Weight formula: base + str
    /// </summary>
    public int MaxWeightBase { get; set; } = 70;
    public int MaxWeightCap { get; set; } = 250;

    /// <summary>
    ///     Base damage formulas by stat group (melee, rogue, caster, archer)
    ///     Formula: stat / divisor
    /// </summary>
    public int MeleeDamageDivisor { get; set; } = 3;      // Str / 3
    public int RogueDamageDivisor { get; set; } = 5;      // Str / 5
    public int CasterDamageDivisor { get; set; } = 3;     // Int / 3
    public int ArcherDamageDivisor { get; set; } = 6;     // Str / 6

    /// <summary>
    ///     Accuracy formulas by stat group
    ///     Formula: stat / divisor
    /// </summary>
    public int MeleeAccuracyDivisor { get; set; } = 3;    // Agi / 3
    public int RogueAccuracyDivisor { get; set; } = 3;    // Agi / 3
    public int CasterAccuracyDivisor { get; set; } = 3;   // Wis / 3
    public int ArcherAccuracyDivisor { get; set; } = 5;   // Agi / 5

    /// <summary>
    ///     Evade formulas by stat group
    ///     Formula: agi / divisor
    /// </summary>
    public int MeleeEvadeDivisor { get; set; } = 5;       // Agi / 5
    public int RogueEvadeDivisor { get; set; } = 3;       // Agi / 3
    public int CasterEvadeDivisor { get; set; } = 4;      // Agi / 4
    public int ArcherEvadeDivisor { get; set; } = 4;      // Agi / 4

    /// <summary>
    ///     Armor formulas by stat group
    ///     Formula: con / divisor
    /// </summary>
    public int MeleeArmorDivisor { get; set; } = 4;       // Con / 4
    public int RogueArmorDivisor { get; set; } = 4;       // Con / 4
    public int CasterArmorDivisor { get; set; } = 5;      // Con / 5
    public int ArcherArmorDivisor { get; set; } = 5;      // Con / 5

    /// <summary>
    ///     Maximum value caps for stats (prevent overflow)
    /// </summary>
    public int StatCap { get; set; } = 32767;  // short.MaxValue
}
