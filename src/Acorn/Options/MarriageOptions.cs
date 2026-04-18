namespace Acorn.Options;

public class MarriageOptions
{
    /// <summary>Gold cost for marriage approval at the law office.</summary>
    public int ApprovalCost { get; set; } = 1000;

    /// <summary>Gold cost for divorce at the law office.</summary>
    public int DivorceCost { get; set; } = 5000;

    /// <summary>Minimum character level required to marry.</summary>
    public int MinLevel { get; set; } = 5;

    /// <summary>Armor item ID required for female characters (wedding dress).</summary>
    public int FemaleArmorId { get; set; } = 451;

    /// <summary>Armor item ID required for male characters (tuxedo).</summary>
    public int MaleArmorId { get; set; } = 452;

    /// <summary>Ring item ID given to both partners after the ceremony.</summary>
    public int RingItemId { get; set; } = 350;

    /// <summary>Music effect ID played during the ceremony.</summary>
    public int MfxId { get; set; } = 10;

    /// <summary>Effect ID for hearts celebration.</summary>
    public int CelebrationEffectId { get; set; } = 10;

    /// <summary>Seconds to wait before the ceremony begins after acceptance.</summary>
    public int CeremonyStartDelaySeconds { get; set; } = 10;

    public static string SectionName => "Marriage";
}
