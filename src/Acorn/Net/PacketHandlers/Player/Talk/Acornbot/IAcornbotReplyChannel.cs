namespace Acorn.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     Sends whisper replies back to a player as if from the Acornbot character.
///     Kept separate from <see cref="IAcornbotService" /> so commands can reply
///     without a circular dependency on the dispatcher.
/// </summary>
public interface IAcornbotReplyChannel
{
    /// <summary>
    ///     The configured bot name used as the whisper sender.
    /// </summary>
    string BotName { get; }

    /// <summary>
    ///     Sends <paramref name="message" /> to <paramref name="playerState" /> as an
    ///     incoming whisper from Acornbot.
    /// </summary>
    Task WhisperAsync(PlayerState playerState, string message);
}
