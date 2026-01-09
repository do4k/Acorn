using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

internal class CharacterTakeClientPacketHandler(
    ILogger<CharacterTakeClientPacketHandler> logger)
    : IPacketHandler<CharacterTakeClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        CharacterTakeClientPacket packet)
    {
        if (playerState.Account is null)
        {
            logger.LogWarning("PlayerState does not have an account associated with it. PlayerId: {PlayerId}",
                playerState.SessionId);
            return;
        }

        // Validate character index is within bounds
        if (packet.CharacterId < 0 || packet.CharacterId >= playerState.Account.Characters.Count())
        {
            logger.LogWarning(
                "Invalid character ID {CharacterId} for account '{Username}' with {CharacterCount} characters",
                packet.CharacterId, playerState.Account.Username, playerState.Account.Characters.Count());
            return;
        }

        // Get the character by index
        var character = playerState.Account.Characters.ElementAt(packet.CharacterId);

        // Store the character index for the confirmation step
        playerState.CharacterIdToDelete = packet.CharacterId;

        // Send back a session ID that the client must echo in the remove request
        await playerState.Send(new CharacterPlayerServerPacket
        {
            SessionId = playerState.SessionId
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (CharacterTakeClientPacket)packet);
    }
}
