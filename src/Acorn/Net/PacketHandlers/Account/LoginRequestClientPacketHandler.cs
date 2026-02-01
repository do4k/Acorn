using System.Text;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Infrastructure.Security;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Account;

public class LoginRequestClientPacketHandler(
    ILogger<LoginRequestClientPacketHandler> logger,
    IDbRepository<Database.Models.Account> repository,
    IPaperdollService paperdollService,
    IWorldQueries world
) : IPacketHandler<LoginRequestClientPacket>
{
    private readonly IPaperdollService _paperdollService = paperdollService;
    private readonly IDbRepository<Database.Models.Account> _repository = repository;
    private readonly IWorldQueries _world = world;

    public async Task HandleAsync(PlayerState playerState,
        LoginRequestClientPacket packet)
    {
        logger.LogDebug("Login attempt for username: {Username}", packet.Username);

        var account = await _repository.GetByKeyAsync(packet.Username);
        if (account is null)
        {
            logger.LogWarning("Login failed - account not found: {Username}", packet.Username);
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
        var valid = Hash.VerifyPassword(packet.Username, packet.Password, salt, account.Password);

        if (valid is false)
        {
            logger.LogWarning("Login failed - invalid password for account: {Username}", packet.Username);
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUserPassword,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUserPassword()
            });
            return;
        }

        logger.LogInformation("Login successful for account: {Username}", packet.Username);
        playerState.Account = account;
        await playerState.Send(new LoginReplyServerPacket
        {
            ReplyCode = LoginReply.Ok,
            ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataOk
            {
                Characters = playerState.Account.Characters
                    .Select((x, id) => x.AsGameModel().AsCharacterListEntry(id, _paperdollService)).ToList()
            }
        });
    }

}