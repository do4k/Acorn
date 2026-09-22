namespace Acorn.Options;

/// <summary>
///     Global list of banned symbols for user-supplied naming text. The policy is
///     shared by character name creation, guild text and Acornbot titles, so an
///     operator maintains exactly one deny list.
/// </summary>
public class BannedTextOptions
{
    /// <summary>
    ///     Substrings that must not appear in user-supplied names, guild text or
    ///     titles. Matched case-insensitively; multi-character entries and emoji
    ///     are allowed. Empty (the default) disables the check.
    /// </summary>
    public List<string> Symbols { get; set; } = [];

    /// <summary>
    ///     The section name in configuration.
    /// </summary>
    public static string SectionName => "BannedText";
}
