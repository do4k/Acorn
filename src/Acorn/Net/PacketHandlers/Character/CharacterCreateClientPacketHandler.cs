using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
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
    IPaperdollService paperdollService,
    ILogger<CharacterCreateClientPacketHandler> logger,
    IOptions<ServerOptions> serverOptions)
    : IPacketHandler<CharacterCreateClientPacket>
{
    private readonly IPaperdollService _paperdollService = paperdollService;
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
            return;
        }

        if (_serverOptions.NewCharacter is null)
        {
            logger.LogError("NewCharacter configuration is missing from server options");
            return;
        }

        // First character in the entire game gets admin privileges, subsequent characters are regular players
        var allCharacters = await repository.GetAllAsync();
        var isFirstCharacterInGame = !allCharacters.Any();

        var character = new Database.Models.Character
        {
            Name = packet.Name,
            Race = packet.Skin,
            Admin = isFirstCharacterInGame ? AdminLevel.HighGameMaster : AdminLevel.Player,
            Accounts_Username = playerState.Account.Username,
            Map = _serverOptions.NewCharacter.Map,
            X = _serverOptions.NewCharacter.X,
            Y = _serverOptions.NewCharacter.Y,
            HairColor = packet.HairColor,
            HairStyle = packet.HairStyle,
            Gender = packet.Gender,
            Hp = 10,
            MaxHp = 10,
            Tp = 10, 
            MaxTp = 10,
            Items = new List<CharacterItem>(),
            Spells = new List<CharacterSpell>(),
            Paperdoll = new CharacterPaperdoll
            {
                CharacterName = packet.Name
            }
        };

        await repository.CreateAsync(character);
        logger.LogInformation("Character '{Name}' created by '{Username}'.", character.Name,
            playerState.Account.Username);

        // Add to account's character list if not already present (EF Core may auto-add via navigation)
        if (!playerState.Account.Characters.Contains(character))
        {
            playerState.Account.Characters.Add(character);
        }

        await playerState.Send(new CharacterReplyServerPacket
        {
            ReplyCode = CharacterReply.Ok,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataOk
            {
                Characters = playerState.Account.Characters
                    .Select((c, id) => c.AsGameModel().AsCharacterListEntry(id, _paperdollService)).ToList()
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (CharacterCreateClientPacket)packet);
    }
}