using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Game.Validation;
using Acorn.Infrastructure.Security;
using Acorn.Infrastructure.Telemetry;
using Acorn.Options;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Account;

public class LoginRequestClientPacketHandler(
    ILogger<LoginRequestClientPacketHandler> logger,
    IDbRepository<Database.Models.Account> repository,
    IPaperdollService paperdollService,
    IWorldQueries world,
    IOptions<ServerOptions> serverOptions,
    AcornMetrics metrics
) : IPacketHandler<LoginRequestClientPacket>
{
    private readonly IPaperdollService _paperdollService = paperdollService;
    private readonly IDbRepository<Database.Models.Account> _repository = repository;
    private readonly IWorldQueries _world = world;
    private readonly ServerOptions _serverOptions = serverOptions.Value;

    public async Task HandleAsync(PlayerState playerState,
        LoginRequestClientPacket packet)
    {
        logger.LogDebug("Login attempt for username: {Username}", packet.Username);

        // Usernames are case-insensitive; normalize before lookup/hashing.
        var username = PlayerValidation.NormalizeName(packet.Username);

        if (!PlayerValidation.IsValidAccountName(username)
            || username.Length < _serverOptions.AccountMinLength
            || username.Length > _serverOptions.AccountMaxLength)
        {
            logger.LoginFailed(packet.Username, "invalid username");
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUser,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUser()
            });
            return;
        }

        if (packet.Password.Length < _serverOptions.PasswordMinLength
            || packet.Password.Length > _serverOptions.PasswordMaxLength)
        {
            logger.LoginFailed(packet.Username, "invalid password length");
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUserPassword,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUserPassword()
            });
            return;
        }

        var account = await _repository.GetByKeyAsync(username);
        if (account is null)
        {
            logger.LoginFailed(packet.Username, "account not found");
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUser,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUser()
            });
            return;
        }

        if (_world.IsPlayerOnline(account.Username))
        {
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.LoggedIn,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataLoggedIn()
            });
            return;
        }

        var salt = Convert.FromBase64String(account.Salt);
        var valid = Hash.VerifyPassword(username, packet.Password, salt, account.Password);

        if (valid is false)
        {
            logger.LoginFailed(packet.Username, "invalid password");
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUserPassword,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUserPassword()
            });
            return;
        }

        logger.LoginSuccessful(packet.Username, playerState.SessionId);
        playerState.Account = account;
        metrics.LoginsTotal.Add(1);
        await playerState.Send(new LoginReplyServerPacket
        {
            ReplyCode = LoginReply.Ok,
            ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataOk
            {
                Characters = playerState.Account.Characters
                    .Select(x => CharacterMapper.FromDatabaseModel(x).AsCharacterListEntry(_paperdollService)).ToList()
            }
        });
    }

}