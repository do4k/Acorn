using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.World.Services.Admin;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

internal class CharacterRemoveClientPacketHandler(
    IDbRepository<Database.Models.Character> repository,
    IPaperdollService paperdollService,
    IAdminCountService adminCountService,
    ILogger<CharacterRemoveClientPacketHandler> logger)
    : IPacketHandler<CharacterRemoveClientPacket>
{
    private readonly IPaperdollService _paperdollService = paperdollService;
    private readonly IAdminCountService _adminCountService = adminCountService;

    public async Task HandleAsync(PlayerState playerState,
        CharacterRemoveClientPacket packet)
    {
        if (playerState.Account is null)
        {
            logger.LogWarning("PlayerState does not have an account associated with it. PlayerId: {PlayerId}",
                playerState.SessionId);
            return;
        }

        // Validate session ID matches
        if (packet.SessionId != playerState.SessionId)
        {
            logger.LogWarning(
                "Session ID mismatch during character deletion. Expected: {Expected}, Got: {Got}",
                playerState.SessionId, packet.SessionId);
            return;
        }

        // Validate character ID matches the one from the take request
        if (playerState.CharacterIdToDelete != packet.CharacterId)
        {
            logger.LogWarning(
                "Character ID mismatch during deletion. Expected: {Expected}, Got: {Got}",
                playerState.CharacterIdToDelete, packet.CharacterId);
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

        var wasAdmin = character.Admin != AdminLevel.Player;

        // Delete the character from the database
        await repository.DeleteAsync(character);

        logger.LogInformation(
            "Character '{CharacterName}' deleted by account '{Username}'",
            character.Name, playerState.Account.Username);

        // Removing an admin character decrements the world admin count.
        if (wasAdmin)
        {
            _adminCountService.Decrement();
        }

        // Remove from the account's character list
        playerState.Account.Characters.Remove(character);

        // Clear the pending delete ID
        playerState.CharacterIdToDelete = null;

        // Send success response with updated character list
        await playerState.Send(new CharacterReplyServerPacket
        {
            ReplyCode = CharacterReply.Deleted,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataDeleted
            {
                Characters = playerState.Account.Characters
                    .Select(c => CharacterMapper.FromDatabaseModel(c).AsCharacterListEntry(_paperdollService)).ToList()
            }
        });
    }

}
