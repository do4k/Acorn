using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Game.Validation;
using Acorn.Infrastructure.Security;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.Models;
using Acorn.Options;
using Acorn.World;
using Acorn.World.Services.Bans;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Account;

[RequiresState(ClientState.Accepted)]
public class LoginRequestClientPacketHandler(
    ILogger<LoginRequestClientPacketHandler> logger,
    IDbRepository<Database.Models.Account> repository,
    IPaperdollService paperdollService,
    IWorldQueries world,
    IBanService banService,
    IAccountLockoutService accountLockout,
    IOptions<ServerOptions> serverOptions,
    AcornMetrics metrics
) : IPacketHandler<LoginRequestClientPacket>
{
    private readonly IPaperdollService _paperdollService = paperdollService;
    private readonly IDbRepository<Database.Models.Account> _repository = repository;
    private readonly IWorldQueries _world = world;
    private readonly IBanService _banService = banService;
    private readonly IAccountLockoutService _accountLockout = accountLockout;
    private readonly ServerOptions _serverOptions = serverOptions.Value;

    public async Task HandleAsync(PlayerState playerState,
        LoginRequestClientPacket packet)
    {
        logger.LogDebug("Login attempt for username: {Username}", packet.Username);

        playerState.LoginAttempts++;

        if (_serverOptions.MaxPlayers > 0 && _world.GetAllPlayers().Count() >= _serverOptions.MaxPlayers)
        {
            logger.LogWarning("Rejecting login from {Origin}: server is full ({MaxPlayers} players)",
                playerState.Communicator.GetConnectionOrigin(), _serverOptions.MaxPlayers);
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.Busy,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataBusy()
            });
            playerState.Disconnect();
            return;
        }

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
            DisconnectIfThrottled(playerState);
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
            DisconnectIfThrottled(playerState);
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
            DisconnectIfThrottled(playerState);
            return;
        }

        if (_banService.IsBanned(BanKeys.Username(account.Username)))
        {
            logger.LoginFailed(packet.Username, "account banned");
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.Banned,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataBanned()
            });
            playerState.Disconnect();
            return;
        }

        if (_world.IsPlayerOnline(account.Username))
        {
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.LoggedIn,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataLoggedIn()
            });
            DisconnectIfThrottled(playerState);
            return;
        }

        // Reject accounts that exhausted their failure budget across connections.
        // The reply mirrors a wrong password so lockout isn't a username oracle.
        if (_accountLockout.IsLockedOut(username))
        {
            logger.LoginFailed(packet.Username, "account locked out");
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUserPassword,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUserPassword()
            });
            playerState.Disconnect();
            return;
        }

        var valid = Hash.VerifyPassword(username, packet.Password, account.Salt, account.Password);

        if (valid is false)
        {
            _accountLockout.RecordFailure(username);
            logger.LoginFailed(packet.Username, "invalid password");
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUserPassword,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUserPassword()
            });
            DisconnectIfThrottled(playerState);
            return;
        }

        _accountLockout.RecordSuccess(username);
        logger.LoginSuccessful(packet.Username, playerState.SessionId);
        playerState.Account = account;
        playerState.LoginAttempts = 0;
        playerState.ClientState = ClientState.LoggedIn;
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

    private void DisconnectIfThrottled(PlayerState playerState)
    {
        if (_serverOptions.MaxLoginAttempts <= 0 ||
            playerState.LoginAttempts < _serverOptions.MaxLoginAttempts)
        {
            return;
        }

        logger.LogWarning(
            "Too many failed login attempts ({Attempts}) from {Origin}; disconnecting",
            playerState.LoginAttempts, playerState.Communicator.GetConnectionOrigin());
        playerState.Disconnect();
    }

}
