namespace Acorn.Game.Services;

/// <summary>
///     Shared "banned symbols" policy for user-supplied naming text: character names,
///     guild tags/names/descriptions and Acornbot titles. The symbol list is global
///     configuration (<c>BannedText:Symbols</c>), so operators maintain it in one place.
/// </summary>
public interface IBannedTextPolicy
{
    /// <summary>
    ///     The configured banned symbols (matched case-insensitively as substrings).
    /// </summary>
    IReadOnlyList<string> Symbols { get; }

    /// <summary>
    ///     Returns the first banned symbol found in <paramref name="text" />.
    ///     Null or empty means the text is allowed; callers should treat both as
    ///     "no violation".
    /// </summary>
    string? FirstViolation(string? text);
}
