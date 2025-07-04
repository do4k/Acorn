using System.Text;
using Acorn.Database.Repository;
using Acorn.Extensions;
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
    WorldState world
) : IPacketHandler<LoginRequestClientPacket>
{
    private readonly ILogger<LoginRequestClientPacketHandler> _logger = logger;
    private readonly IDbRepository<Database.Models.Account> _repository = repository;
    private readonly WorldState _world = world;

    public async Task HandleAsync(PlayerState playerState,
        LoginRequestClientPacket packet)
    {
        var account = await _repository.GetByKeyAsync(packet.Username);
        if (account is null)
        {
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUser,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUser()
            });
            return;
        }

        if (_world.LoggedIn(account.Username))
        {
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.LoggedIn,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataLoggedIn()
            });
            return;
        }

        var salt = Encoding.UTF8.GetBytes(account.Salt);
        var valid = Hash.VerifyPassword(packet.Username, packet.Password, salt, account.Password);

        if (valid is false)
        {
            await playerState.Send(new LoginReplyServerPacket
            {
                ReplyCode = LoginReply.WrongUserPassword,
                ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataWrongUserPassword()
            });
            return;
        }

        playerState.Account = account;
        await playerState.Send(new LoginReplyServerPacket
        {
            ReplyCode = LoginReply.Ok,
            ReplyCodeData = new LoginReplyServerPacket.ReplyCodeDataOk
            {
                Characters = playerState.Account.Characters
                    .Select((x, id) => x.AsCharacterListEntry(id)).ToList()
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (LoginRequestClientPacket)packet);
    }
}