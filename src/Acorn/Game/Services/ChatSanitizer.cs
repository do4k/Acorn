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

public class ChatSanitizer(IOptions<ServerOptions> serverOptions) : IChatSanitizer
{
    public string Sanitize(string message, string? senderName = null)
    {
        var options = serverOptions.Value;

        var capped = ChatText.LimitLength(message, options.ChatLength);

        var prefixWidth = senderName is null
            ? 0
            : ChatText.Width(ChatText.UcFirst(senderName) + "  ");

        return ChatText.Cap(capped, options.ChatMaxWidth - prefixWidth);
    }
}

/// <summary>
///     Helpers for capping Endless Online chat text using the client's bitmap font
///     metrics. Ported from eoserv's util::text_width / util::text_cap.
/// </summary>
internal static class ChatText
{
    private static readonly int[] Sizes =
    [
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, // NUL -  SI
         3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, // DLE -  US
         3,  3,  5,  7,  6,  8,  6,  2,  3,  3,  4,  6,  3,  3,  3,  5, // ' ' - '/'
         6,  6,  6,  6,  6,  6,  6,  6,  6,  6,  3,  3,  6,  6,  6,  6, // '0' - '?'
        11,  7,  7,  7,  8,  7,  6,  8,  8,  3,  5,  7,  6,  9,  8,  8, // '@' - 'O'
         7,  8,  8,  7,  7,  8,  7, 11,  7,  7,  7,  3,  5,  3,  6,  6, // 'P' - '_'
         3,  6,  6,  6,  6,  6,  3,  6,  6,  2,  2,  6,  2,  8,  6,  6, // '`' - 'o'
         6,  6,  3,  5,  3,  6,  6,  8,  5,  5,  5,  4,  2,  4,  7,  3, // 'p' - DEL
         0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
         0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
         3,  3,  6,  6,  6,  6,  2,  6,  3,  9,  4,  6,  6,  3,  8,  6,
         4,  6,  3,  3,  3,  6,  6,  3,  3,  3,  4,  6,  8,  8,  8,  6,
         7,  7,  7,  7,  7,  7, 10,  7,  7,  7,  7,  7,  3,  3,  3,  3,
         8,  8,  8,  8,  8,  8,  8,  6,  8,  8,  8,  8,  8,  7,  7,  6,
         6,  6,  6,  6,  6,  6, 10,  6,  6,  6,  6,  6,  2,  4,  4,  4,
         6,  6,  6,  6,  6,  6,  6,  6,  6,  6,  6,  6,  6,  5,  6,  5
    ];

    /// <summary>Rendered pixel width of <paramref name="text" />.</summary>
    public static int Width(string text)
    {
        var width = 0;
        foreach (var c in text)
        {
            width += Sizes[(byte)c];
        }

        return width;
    }

    /// <summary>
    ///     Truncates <paramref name="text" /> to the longest prefix whose rendered
    ///     width does not exceed <paramref name="maxWidth" />.
    /// </summary>
    public static string Cap(string text, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return string.Empty;
        }

        var width = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var next = width + Sizes[(byte)text[i]];
            if (next > maxWidth)
            {
                return text[..i];
            }

            width = next;
        }

        return text;
    }

    /// <summary>
    ///     Truncates a message to <paramref name="maxLength" /> characters using the
    ///     same " [...]" suffix convention as eoserv's limit_message.
    /// </summary>
    public static string LimitLength(string message, int maxLength)
    {
        if (maxLength <= 0 || message.Length <= maxLength)
        {
            return message;
        }

        return maxLength <= 6
            ? message[..maxLength]
            : message[..(maxLength - 6)] + " [...]";
    }

    /// <summary>Uppercases the first character of <paramref name="text" />.</summary>
    public static string UcFirst(string text)
    {
        return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
