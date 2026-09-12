using Acorn.Options;
using Microsoft.Extensions.Options;

namespace Acorn.Game.Services;

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
