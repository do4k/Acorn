namespace Acorn.Options;

public class JukeboxOptions
{
    public static string SectionName => "Jukebox";

    /// <summary>
    ///     Gold cost to play a jukebox track.
    /// </summary>
    public int Cost { get; set; } = 100;

    /// <summary>
    ///     Maximum track ID that can be played.
    /// </summary>
    public int MaxTrackId { get; set; } = 30;

    /// <summary>
    ///     Duration a track plays in ticks before the jukebox is available again.
    /// </summary>
    public int TrackTimer { get; set; } = 60;

    /// <summary>
    ///     Maximum note ID for bard instruments.
    /// </summary>
    public int MaxNoteId { get; set; } = 36;

    /// <summary>
    ///     Item IDs that are considered instruments (spec1 values on weapons).
    /// </summary>
    public List<int> InstrumentItems { get; set; } = [1, 2, 3];
}
