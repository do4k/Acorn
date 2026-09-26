using Acorn.Database.Repository;
using Acorn.Game.Validation;
using Acorn.Infrastructure.Security;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.Models;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Account;

/// <summary>
///     Handles password change requests (Account_Agree).
///     Matches reoserv account.rs account_agree handler.
/// </summary>
[RequiresState(ClientState.LoggedIn)]
internal class AccountAgreeClientPacketHandler(
    IDbRepository<Database.Models.Account> accountRepository,
    ILogger<AccountAgreeClientPacketHandler> logger,
    IOptions<ServerOptions> serverOptions
) : IPacketHandler<AccountAgreeClientPacket>
{
    private readonly ServerOptions _serverOptions = serverOptions.Value;

    public async Task HandleAsync(PlayerState playerState, AccountAgreeClientPacket packet)
    {
        // Must be logged in (account set during login)
        if (playerState.Account is null)
        {
            return;
        }

        var username = PlayerValidation.NormalizeName(packet.Username);

        // The change must target the caller's own account. The packet username is
        // client-controlled and must never select a different account, even when
        // the caller knows that account's old password.
        if (!string.Equals(username, playerState.Account.Username, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Password change for {Username} rejected: does not match session account {SessionAccount}",
                packet.Username, playerState.Account.Username);
            await playerState.Send(new AccountReplyServerPacket
            {
                ReplyCode = AccountReply.ChangeFailed,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataChangeFailed()
            });
            return;
        }

        var account = await accountRepository.GetByKeyAsync(username);
        if (account is null)
        {
            logger.LogWarning("Password change failed for {Username}: account not found", packet.Username);
            await playerState.Send(new AccountReplyServerPacket
            {
                ReplyCode = AccountReply.Exists,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataExists()
            });
            return;
        }

        // Verify old password
        var valid = Hash.VerifyPassword(username, packet.OldPassword, account.Salt, account.Password);

        if (!valid)
        {
            logger.LogWarning("Password change failed for {Username}: invalid old password", packet.Username);
            await playerState.Send(new AccountReplyServerPacket
            {
                ReplyCode = AccountReply.ChangeFailed,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataChangeFailed()
            });
            return;
        }

        // Enforce the same password policy as account creation. Without this a
        // 1-character (or multi-megabyte, CPU-burning) password could be stored.
        if (packet.NewPassword.Length < _serverOptions.PasswordMinLength ||
            packet.NewPassword.Length > _serverOptions.PasswordMaxLength)
        {
            logger.LogWarning("Password change failed for {Username}: new password length invalid",
                packet.Username);
            await playerState.Send(new AccountReplyServerPacket
            {
                ReplyCode = AccountReply.ChangeFailed,
                ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataChangeFailed()
            });
            return;
        }

        // Generate new password hash
        var newHash = Hash.HashPassword(username, packet.NewPassword,
            _serverOptions.PasswordHashIterations, out var newSalt);
        account.Password = newHash;
        account.Salt = newSalt;

        await accountRepository.UpdateAsync(account);

        logger.PasswordChanged(packet.Username);
        await playerState.Send(new AccountReplyServerPacket
        {
            ReplyCode = AccountReply.Changed,
            ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataChanged()
        });
    }
}
