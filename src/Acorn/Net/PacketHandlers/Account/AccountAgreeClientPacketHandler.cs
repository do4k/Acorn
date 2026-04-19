using Acorn.Database.Repository;
using Acorn.Infrastructure.Security;
using Acorn.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Account;

/// <summary>
///     Handles password change requests (Account_Agree).
///     Matches reoserv account.rs account_agree handler.
/// </summary>
internal class AccountAgreeClientPacketHandler(
    IDbRepository<Database.Models.Account> accountRepository,
    ILogger<AccountAgreeClientPacketHandler> logger
) : IPacketHandler<AccountAgreeClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, AccountAgreeClientPacket packet)
    {
        // Must be logged in (account set during login)
        if (playerState.Account is null)
        {
            return;
        }

        var account = await accountRepository.GetByKeyAsync(packet.Username);
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
        var salt = Convert.FromBase64String(account.Salt);
        var valid = Hash.VerifyPassword(packet.Username, packet.OldPassword, salt, account.Password);

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

        // Generate new password hash
        var newHash = Hash.HashPassword(packet.Username, packet.NewPassword, out var newSalt);
        account.Password = newHash;
        account.Salt = Convert.ToBase64String(newSalt);

        await accountRepository.UpdateAsync(account);

        logger.PasswordChanged(packet.Username);
        await playerState.Send(new AccountReplyServerPacket
        {
            ReplyCode = AccountReply.Changed,
            ReplyCodeData = new AccountReplyServerPacket.ReplyCodeDataChanged()
        });
    }
}
