using Acorn.Options;
using Microsoft.Extensions.Options;

namespace Acorn.Game.Services;

/// <summary>
///     Applies chat length and rendered-width caps to outgoing messages.
/// </summary>
public interface IChatSanitizer
{
    /// <summary>
    ///     Caps a chat message to the configured maximum length (characters) and
    ///     rendered width (pixels). When <paramref name="senderName" /> is provided
    ///     the width budget is reduced by the width of the "Name  " prefix, matching
    ///     eoserv's per-channel chat capping.
    /// </summary>
    string Sanitize(string message, string? senderName = null);
}
