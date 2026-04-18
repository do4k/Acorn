using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Account;

internal class AccountCreateClientPacketHandler(
    IDbRepository<Database.Models.Account> accountRepository,
    ILogger<AccountCreateClientPacketHandler> logger,
    UtcNowDelegate nowDelegate,
    AcornMetrics metrics
) : IPacketHandler<AccountCreateClientPacket>
{
    private readonly IDbRepository<Database.Models.Account> _accountRepository = accountRepository;
    private readonly ILogger<AccountCreateClientPacketHandler> _logger = logger;
    private readonly AcornMetrics _metrics = metrics;

    public async Task HandleAsync(PlayerState playerState,
        AccountCreateClientPacket packet)
    {
        var account = await _accountRepository.GetByKeyAsync(packet.Username);
        if (account is not null)
        {
            _logger.LogDebug("Account with username {Username} already exists...", packet.Username);
            await playerState.Send(
                new AccountReplyServerPacket
                {
                    ReplyCode = AccountReply.Exists,
                    ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataExists()
                });
            return;
        }

        var newAccount = packet.AsNewAccount(nowDelegate());
        await _accountRepository.CreateAsync(newAccount);

        _metrics.AccountsCreated.Add(1);
        _logger.AccountCreated(packet.Username);
        await playerState.Send(new AccountReplyServerPacket
        {
            ReplyCode = AccountReply.Created,
            ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataCreated()
        });
    }

}