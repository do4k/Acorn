namespace Acorn.Options;

/// <summary>
///     Configuration for the game data pub files (ECF/EIF/ENF/ESF) and maps.
/// </summary>
public class DataOptions
{
    public static string SectionName => "Data";

    /// <summary>Path to the ECF (classes) pub file, relative to the working directory.</summary>
    public string EcfFile { get; set; } = "Data/Pub/dat001.ecf";

    /// <summary>Path to the EIF (items) pub file, relative to the working directory.</summary>
    public string EifFile { get; set; } = "Data/Pub/dat001.eif";

    /// <summary>Path to the ENF (NPCs) pub file, relative to the working directory.</summary>
    public string EnfFile { get; set; } = "Data/Pub/dtn001.enf";

    /// <summary>Path to the ESF (spells) pub file, relative to the working directory.</summary>
    public string EsfFile { get; set; } = "Data/Pub/dsl001.esf";

    /// <summary>Directory containing the .emf map files.</summary>
    public string MapsPath { get; set; } = "Data/Maps";
}
