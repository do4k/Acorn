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

    public async Task HandleAsync(ConnectionHandler connectionHandler,
        AccountRequestClientPacket packet)
    {
        var account = await _accountRepository.GetByKeyAsync(packet.Username);
        if (account is not null)
        {
            _logger.LogDebug("Account exists {username}", account.Username);
            await connectionHandler.Send(new AccountReplyServerPacket
            {
                ReplyCode = AccountReply.Exists,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataExists()
            });
        }
        else
        {
            _logger.LogDebug("Account \"{username}\" does not exist", packet.Username);

            if (connectionHandler.StartSequence.Value > EoNumericLimits.CHAR_MAX)
            {
                connectionHandler.StartSequence = InitSequenceStart.Generate(connectionHandler.Rnd);
            }

            await connectionHandler.Send(new AccountReplyServerPacket
            {
                ReplyCode = (AccountReply)connectionHandler.SessionId,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataDefault
                {
                    SequenceStart = connectionHandler.StartSequence.Seq1
                }
            });
        }
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (AccountRequestClientPacket)packet);
    }
}