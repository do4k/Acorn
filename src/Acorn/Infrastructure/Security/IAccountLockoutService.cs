namespace Acorn.Infrastructure.Security;

/// <summary>
///     Tracks failed login attempts per account and temporarily locks accounts that
///     exceed the configured threshold, so an attacker cannot reconnect and retry
///     passwords indefinitely after the per-connection limit disconnects them.
/// </summary>
public interface IAccountLockoutService
{
    /// <summary>
    ///     Whether the account is currently locked out from logging in.
    /// </summary>
    bool IsLockedOut(string username);

    /// <summary>
    ///     Records a failed login for the account.
    /// </summary>
    void RecordFailure(string username);

    /// <summary>
    ///     Clears any tracked failures for the account after a successful login.
    /// </summary>
    void RecordSuccess(string username);
}
