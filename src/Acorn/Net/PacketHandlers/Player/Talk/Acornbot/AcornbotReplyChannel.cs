using Acorn.Options;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     Default <see cref="IAcornbotReplyChannel" />: wraps outgoing text in a
///     <c>TalkTellServerPacket</c> authored by the configured bot name, so the
///     client renders replies in the whisper window next to the player's commands.
/// </summary>
public class AcornbotReplyChannel(IOptions<AcornbotOptions> options) : IAcornbotReplyChannel
{
    private readonly AcornbotOptions _options = options.Value;

    public string BotName => _options.Name;

    public Task WhisperAsync(PlayerState playerState, string message)
    {
        return playerState.Send(new TalkTellServerPacket
        {
            Message = message,
            PlayerName = _options.Name
        });
    }
}
