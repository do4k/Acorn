using Acorn.Database.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Admin;

/// <summary>
///     Tracks how many admin characters exist in the world. Mirrors eoserv's
///     <c>World::admin_count</c>, which gates whether the first character created
///     is granted HGM status. The count is loaded from the database on first use
///     and then maintained in memory as characters are created and deleted.
/// </summary>
public interface IAdminCountService
{
    /// <summary>
    ///     Gets the current number of admin characters, loading it from the
    ///     database the first time it is requested.
    /// </summary>
    Task<int> GetAdminCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Records that an admin character has been created.</summary>
    void Increment();

    /// <summary>Records that an admin character has been removed.</summary>
    void Decrement();
}
