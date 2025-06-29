using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

internal class CharacterCreateClientPacketHandler(
    IDbRepository<Database.Models.Character> repository,
    ILogger<CharacterCreateClientPacketHandler> logger,
    IOptions<ServerOptions> gameOptions)
    : IPacketHandler<CharacterCreateClientPacket>
{
    private readonly ServerOptions _serverOptions = gameOptions.Value;

    public async Task HandleAsync(ConnectionHandler connectionHandler,
        CharacterCreateClientPacket packet)
    {
        var characterQuery = await repository.GetByKeyAsync(packet.Name);
        var exists = characterQuery is not null;

        if (exists)
        {
            await connectionHandler.Send(
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
            Admin = packet.Name.ToLower() switch
            {
                "danzo" => AdminLevel.HighGameMaster,
                _ => (int)AdminLevel.Player
            },
            Accounts_Username = connectionHandler.Account?.Username ??
                throw new InvalidOperationException("Cannot create a character without a user"),
            Map = _serverOptions.NewCharacter.Map,
            X = _serverOptions.NewCharacter.X,
            Y = _serverOptions.NewCharacter.Y,
            HairColor = packet.HairColor,
            HairStyle = packet.HairStyle,
            Gender = packet.Gender
        };

        await repository.CreateAsync(character);
        logger.LogInformation("Character '{Name}' created by '{Username}'.", character.Name,
            connectionHandler.Account.Username);
        connectionHandler.Account.Characters.Add(character);

        await connectionHandler.Send(new CharacterReplyServerPacket
        {
            ReplyCode = CharacterReply.Ok,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataOk
            {
                Characters = connectionHandler.Account.Characters.Select((c, id) => c.AsCharacterListEntry(id)).ToList()
            }
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (CharacterCreateClientPacket)packet);
    }
}