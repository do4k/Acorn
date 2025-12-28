namespace Acorn.Options;

/// <summary>
/// Configuration options for player respawn location when they die.
/// This is the rescue/fallback location when a player has no home set.
/// </summary>
public class RescueOptions
{
    public required int Map { get; set; }
    public required int X { get; set; }
    public required int Y { get; set; }

    public static string SectionName => "Rescue";
}
