using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
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

    public async Task HandleAsync(PlayerState playerState,
        AccountRequestClientPacket packet)
    {
        var account = await _accountRepository.GetByKeyAsync(packet.Username);
        if (account is not null)
        {
            _logger.LogDebug("Account exists {username}", account.Username);
            await playerState.Send(new AccountReplyServerPacket
            {
                ReplyCode = AccountReply.Exists,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataExists()
            });
        }
        else
        {
            _logger.LogDebug("Account \"{username}\" does not exist", packet.Username);

            // Send back the current sequence start (matches reoserv: get_start(), no regeneration)
            await playerState.Send(new AccountReplyServerPacket
            {
                ReplyCode = (AccountReply)playerState.SessionId,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataDefault
                {
                    SequenceStart = playerState.Sequencer.GetStart()
                }
            });
        }
    }

}