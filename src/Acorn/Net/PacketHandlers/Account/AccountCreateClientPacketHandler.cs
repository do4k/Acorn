using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Validation;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.Models;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Account;

[RequiresState(ClientState.Accepted)]
internal class AccountCreateClientPacketHandler(
    IDbRepository<Database.Models.Account> accountRepository,
    ILogger<AccountCreateClientPacketHandler> logger,
    UtcNowDelegate nowDelegate,
    IOptions<ServerOptions> serverOptions,
    AcornMetrics metrics
) : IPacketHandler<AccountCreateClientPacket>
{
    private readonly IDbRepository<Database.Models.Account> _accountRepository = accountRepository;
    private readonly ILogger<AccountCreateClientPacketHandler> _logger = logger;
    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private readonly AcornMetrics _metrics = metrics;

    public async Task HandleAsync(PlayerState playerState,
        AccountCreateClientPacket packet)
    {
        var username = PlayerValidation.NormalizeName(packet.Username);

        if (!PlayerValidation.IsValidAccountName(username)
            || username.Length < _serverOptions.AccountMinLength
            || username.Length > _serverOptions.AccountMaxLength
            || packet.Password.Length < _serverOptions.PasswordMinLength
            || packet.Password.Length > _serverOptions.PasswordMaxLength)
        {
            _logger.LogDebug("Rejecting account creation for invalid username or password");
            await playerState.Send(
                new AccountReplyServerPacket
                {
                    ReplyCode = AccountReply.NotApproved,
                    ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataNotApproved()
                });
            return;
        }

        var account = await _accountRepository.GetByKeyAsync(username);
        if (account is not null)
        {
            _logger.LogDebug("Account with username {Username} already exists...", username);
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
        _logger.AccountCreated(newAccount.Username);
        await playerState.Send(new AccountReplyServerPacket
        {
            ReplyCode = AccountReply.Created,
            ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataCreated()
        });
    }

}
