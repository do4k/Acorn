using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Game.Validation;
using Acorn.Infrastructure.Telemetry;
using Acorn.Options;
using Acorn.World.Services.Admin;
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
    IOptions<ServerOptions> serverOptions,
    IAdminCountService adminCountService,
    AcornMetrics metrics)
    : IPacketHandler<CharacterCreateClientPacket>
{
    private readonly IPaperdollService _paperdollService = paperdollService;
    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private readonly IAdminCountService _adminCountService = adminCountService;

    public async Task HandleAsync(PlayerState playerState,
        CharacterCreateClientPacket packet)
    {
        if (playerState.Account is null)
        {
            logger.LogWarning("PlayerState does not have an account associated with it. PlayerId: {PlayerId}",
                playerState.SessionId);
            return;
        }

        var name = PlayerValidation.NormalizeName(packet.Name);

        if (!IsValidAppearance(packet))
        {
            logger.LogDebug("Rejecting character creation for invalid appearance. Account: {Username}",
                playerState.Account.Username);
            await SendNotApproved(playerState);
            return;
        }

        if (playerState.Account.Characters.Count >= _serverOptions.MaxCharacters)
        {
            logger.LogDebug("Account {Username} is full, cannot create another character",
                playerState.Account.Username);
            await playerState.Send(new CharacterReplyServerPacket
            {
                ReplyCode = CharacterReply.Full,
                ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataFull()
            });
            return;
        }

        if (!PlayerValidation.IsValidCharacterName(name))
        {
            logger.LogDebug("Rejecting character creation for invalid name. Account: {Username}",
                playerState.Account.Username);
            await SendNotApproved(playerState);
            return;
        }

        var characterQuery = await repository.GetByKeyAsync(name);
        if (characterQuery is not null)
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

        // The first character in the game is only granted admin when no admin
        // characters exist and the server allows it.
        var adminCount = await _adminCountService.GetAdminCountAsync();
        var grantAdmin = _serverOptions.FirstCharacterAdmin && adminCount == 0;

        var character = new Database.Models.Character
        {
            Name = name,
            Race = packet.Skin,
            Admin = grantAdmin ? AdminLevel.HighGameMaster : AdminLevel.Player,
            Accounts_Username = playerState.Account.Username,
            Map = _serverOptions.NewCharacter.Map,
            X = _serverOptions.NewCharacter.X,
            Y = _serverOptions.NewCharacter.Y,
            HairColor = packet.HairColor,
            HairStyle = packet.HairStyle,
            Gender = packet.Gender,
            Class = 1,
            Hp = 10,
            MaxHp = 10,
            Tp = 10,
            MaxTp = 10,
            Sp = 20,
            MaxSp = 20,
            Items = new List<CharacterItem>(),
            Spells = new List<CharacterSpell>(),
            Paperdoll = new CharacterPaperdoll
            {
                CharacterName = name
            }
        };

        await repository.CreateAsync(character);

        if (grantAdmin)
        {
            _adminCountService.Increment();
        }

        metrics.CharactersCreated.Add(1);
        logger.CharacterCreated(character.Name, playerState.Account.Username);

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
                    .Select(c => CharacterMapper.FromDatabaseModel(c).AsCharacterListEntry(_paperdollService)).ToList()
            }
        });
    }

    private bool IsValidAppearance(CharacterCreateClientPacket packet)
    {
        if (packet.Gender is not (Gender.Female or Gender.Male))
        {
            return false;
        }

        return packet.HairStyle >= _serverOptions.CreateMinHairStyle
               && packet.HairStyle <= _serverOptions.CreateMaxHairStyle
               && packet.HairColor >= _serverOptions.CreateMinHairColor
               && packet.HairColor <= _serverOptions.CreateMaxHairColor
               && packet.Skin >= _serverOptions.CreateMinSkin
               && packet.Skin <= _serverOptions.CreateMaxSkin;
    }

    private static Task SendNotApproved(PlayerState playerState) =>
        playerState.Send(new CharacterReplyServerPacket
        {
            ReplyCode = CharacterReply.NotApproved,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataNotApproved()
        });
}
