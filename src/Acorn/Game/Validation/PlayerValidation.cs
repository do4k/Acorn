namespace Acorn.Game.Validation;

/// <summary>
///     Name and password validation rules shared by the account and character
///     packet handlers. Mirrors eoserv's <c>Player::ValidName</c> and
///     <c>Character::ValidName</c> behaviour (all names are stored lowercase).
/// </summary>
public static class PlayerValidation
{
    /// <summary>
    ///     Character names that are never allowed, matching eoserv.
    /// </summary>
    private static readonly HashSet<string> ReservedCharacterNames =
        new(StringComparer.Ordinal) { "server" };

    /// <summary>
    ///     Normalizes a username or character name the same way eoserv does
    ///     (lowercase). The client may send mixed case; all persistence and
    ///     lookups are case-insensitive.
    /// </summary>
    public static string NormalizeName(string name) => name.ToLowerInvariant();

    /// <summary>
    ///     Validates the character set of an account username: lowercase ASCII
    ///     letters, spaces and digits only. Length bounds are checked separately
    ///     using the configured account length options.
    /// </summary>
    public static bool IsValidAccountName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return name.All(IsAllowedNameCharacter);
    }

    /// <summary>
    ///     Validates a character name: 4-12 characters, lowercase ASCII letters,
    ///     spaces or digits, and not reserved. Mirrors eoserv's
    ///     <c>Character::ValidName</c> (with spaces/digits additionally allowed).
    /// </summary>
    public static bool IsValidCharacterName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length is < 4 or > 12)
        {
            return false;
        }

        if (!name.All(IsAllowedNameCharacter))
        {
            return false;
        }

        // Reject names that are only whitespace.
        if (name.Trim().Length == 0)
        {
            return false;
        }

        return !ReservedCharacterNames.Contains(name);
    }

    private static bool IsAllowedNameCharacter(char c) =>
        c is >= 'a' and <= 'z' or >= '0' and <= '9' or ' ';
}
