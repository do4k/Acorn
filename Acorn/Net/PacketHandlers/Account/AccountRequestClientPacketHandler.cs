using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Account;

internal class AccountRequestClientPacketHandler(
    IDbRepository<Database.Models.Account> accountRepository,
    ILogger<AccountRequestClientPacket> logger
) : IPacketHandler<AccountRequestClientPacket>
{
    private readonly IDbRepository<Database.Models.Account> _accountRepository = accountRepository;
    private readonly ILogger<AccountRequestClientPacket> _logger = logger;

    public async Task HandleAsync(PlayerConnection playerConnection,
        AccountRequestClientPacket packet)
    {
        var account = await _accountRepository.GetByKeyAsync(packet.Username);
        if (account is not null)
        {
            _logger.LogDebug("Account exists {username}", account.Username);
            await playerConnection.Send(new AccountReplyServerPacket
            {
                ReplyCode = AccountReply.Exists,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataExists()
            });
        }
        else
        {
            _logger.LogDebug("Account \"{username}\" does not exist", packet.Username);

            if (playerConnection.StartSequence.Value > EoNumericLimits.CHAR_MAX)
            {
                playerConnection.StartSequence = InitSequenceStart.Generate(playerConnection.Rnd);
            }

            await playerConnection.Send(new AccountReplyServerPacket
            {
                ReplyCode = (AccountReply)playerConnection.SessionId,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataDefault
                {
                    SequenceStart = playerConnection.StartSequence.Seq1
                }
            });
        }
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (AccountRequestClientPacket)packet);
    }
}