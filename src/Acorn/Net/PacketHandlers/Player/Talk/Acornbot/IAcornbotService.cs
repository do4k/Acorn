using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     Entry point for the Acornbot PM service. Whispers addressed to the
///     configured bot name are parsed here and dispatched to the registered
///     <see cref="IAcornbotCommand" /> set.
/// </summary>
public interface IAcornbotService
{
    /// <summary>
    ///     Whether <paramref name="whisperTarget" /> addresses the Acornbot and the
    ///     service is enabled. The bot name is matched case-insensitively because
    ///     the client lower-cases whisper targets.
    /// </summary>
    bool IsBotName(string whisperTarget);

    /// <summary>
    ///     Handles <paramref name="message" /> as a command whisper from
    ///     <paramref name="playerState" /> to Acornbot. Always consumes the whisper
    ///     (unknown commands receive a help reply) so the caller should not fall
    ///     back to normal player lookup.
    /// </summary>
    Task HandleWhisperAsync(PlayerState playerState, string message);
}
