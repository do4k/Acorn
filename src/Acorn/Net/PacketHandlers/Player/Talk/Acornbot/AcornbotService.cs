using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     Default <see cref="IAcornbotService" />. Parses the whispered text - with or
///     without a leading "!acornbot" handle - into a command name plus arguments and
///     routes it to a matching <see cref="IAcornbotCommand" />.
/// </summary>
public class AcornbotService(
    IEnumerable<IAcornbotCommand> commands,
    IAcornbotReplyChannel replies,
    IOptions<AcornbotOptions> options,
    ILogger<AcornbotService> logger) : IAcornbotService
{
    private readonly AcornbotOptions _options = options.Value;

    public bool IsBotName(string whisperTarget)
    {
        return _options.Enabled
               && !string.IsNullOrWhiteSpace(whisperTarget)
               && string.Equals(whisperTarget.Trim(), _options.Name, StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleWhisperAsync(PlayerState playerState, string message)
    {
        var text = StripHandle(message.Trim());

        if (text.Length == 0)
        {
            await HelpAsync(playerState);
            return;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0];

        if (command.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            await HelpAsync(playerState);
            return;
        }

        var handler = commands.FirstOrDefault(x => x.CanHandle(command));
        if (handler is null)
        {
            await replies.WhisperAsync(playerState,
                $"Unknown command \"{command}\". Send \"help\" to see what I can do.");
            logger.LogDebug("Unknown Acornbot command {Command} from {Player}",
                command, playerState.Character?.Name);
            return;
        }

        await handler.HandleAsync(playerState, command, parts[1..]);
    }

    /// <summary>
    ///     Removes a redundant "!acornbot" prefix from the whisper body. The eoweb
    ///     client strips the handle itself when a player types "!acornbot ..." in
    ///     local chat, but the vanilla PM window sends whatever was typed.
    /// </summary>
    private string StripHandle(string text)
    {
        if (!text.StartsWith('!'))
        {
            return text;
        }

        var space = text.IndexOf(' ');
        var handle = space < 0 ? text[1..] : text[1..space];
        if (!handle.Equals(_options.Name, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return space < 0 ? string.Empty : text[(space + 1)..].Trim();
    }

    private async Task HelpAsync(PlayerState playerState)
    {
        await replies.WhisperAsync(playerState, $"Hi! I'm {replies.BotName}. Send me a command:");

        foreach (var command in commands)
        {
            var usage = string.IsNullOrEmpty(command.Usage)
                ? command.Commands[0]
                : $"{command.Commands[0]} {command.Usage}";
            await replies.WhisperAsync(playerState, $"  {usage} - {command.Description}");
        }
    }
}
