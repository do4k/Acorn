using Acorn.Options;
using Microsoft.Extensions.Options;

namespace Acorn.Game.Services;

/// <summary>
///     Default <see cref="IBannedTextPolicy" />. Symbols are matched as
///     case-insensitive substrings so single characters, emoji and multi-character
///     sequences all work.
/// </summary>
public class BannedTextPolicy(IOptions<BannedTextOptions> options) : IBannedTextPolicy
{
    private readonly BannedTextOptions _options = options.Value;

    public IReadOnlyList<string> Symbols => _options.Symbols;

    public string? FirstViolation(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return _options.Symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .FirstOrDefault(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));
    }
}
