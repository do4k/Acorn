using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

internal class CharacterCreateClientPacketHandler(
    IDbRepository<Database.Models.Character> repository,
    ILogger<CharacterCreateClientPacketHandler> logger,
    IOptions<ServerOptions> serverOptions)
    : IPacketHandler<CharacterCreateClientPacket>
{
    private readonly ServerOptions _serverOptions = serverOptions.Value;

    public async Task HandleAsync(PlayerState playerState,
        CharacterCreateClientPacket packet)
    {
        if (playerState.Account is null)
        {
            logger.LogWarning("PlayerState does not have an account associated with it. PlayerId: {PlayerId}",
                playerState.SessionId);
            return;
        }
        
        var characterQuery = await repository.GetByKeyAsync(packet.Name);
        var exists = characterQuery is not null;

        if (exists)
        {
            await playerState.Send(
                new CharacterReplyServerPacket
                {
                    ReplyCode = CharacterReply.Exists,
                    ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataExists()
                });
        }

        var character = new Database.Models.Character
        {
            Name = packet.Name,
            Race = packet.Skin,
            Admin = AdminLevel.HighGameMaster,
            Accounts_Username = playerState.Account.Username,
            Map = _serverOptions.NewCharacter.Map,
            X = _serverOptions.NewCharacter.X,
            Y = _serverOptions.NewCharacter.Y,
            HairColor = packet.HairColor,
            HairStyle = packet.HairStyle,
            Gender = packet.Gender
        };

        await repository.CreateAsync(character);
        logger.LogInformation("Character '{Name}' created by '{Username}'.", character.Name,
            playerState.Account.Username);
        playerState.Account.Characters.Add(character);

        await playerState.Send(new CharacterReplyServerPacket
        {
            ReplyCode = CharacterReply.Ok,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataOk
            {
                Characters = playerState.Account.Characters.Select((c, id) => c.AsCharacterListEntry(id)).ToList()
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (CharacterCreateClientPacket)packet);
    }
}