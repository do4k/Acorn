using Acorn.Net.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

[RequiresState(ClientState.LoggedIn)]
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

        // Resolve the character by its stable database id (not list index).
        var character = playerState.Account.Characters
            .FirstOrDefault(c => c.Id == packet.CharacterId);

        if (character is null)
        {
            logger.LogWarning(
                "Invalid character ID {CharacterId} for account '{Username}' with {CharacterCount} characters",
                packet.CharacterId, playerState.Account.Username, playerState.Account.Characters.Count);
            return;
        }

        // Store the character id for the confirmation step.
        playerState.CharacterIdToDelete = character.Id;

        // Send back a session ID and the character id that the client must echo
        // in the remove request.
        await playerState.Send(new CharacterPlayerServerPacket
        {
            SessionId = playerState.SessionId,
            CharacterId = character.Id
        });
    }

}
