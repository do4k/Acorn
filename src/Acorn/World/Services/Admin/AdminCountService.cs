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

/// <inheritdoc />
public class AdminCountService(
    IServiceScopeFactory scopeFactory,
    ILogger<AdminCountService> logger) : IAdminCountService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int? _adminCount;

    public async Task<int> GetAdminCountAsync(CancellationToken cancellationToken = default)
    {
        if (_adminCount is not null)
        {
            return _adminCount.Value;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_adminCount is not null)
            {
                return _adminCount.Value;
            }

            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider
                .GetRequiredService<IDbRepository<Database.Models.Character>>();
            var characters = await repository.GetAllAsync();
            _adminCount = characters.Count(c => c.Admin != AdminLevel.Player);
            logger.LogDebug("Initialized admin count to {AdminCount}", _adminCount);
            return _adminCount.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Increment()
    {
        _adminCount = _adminCount is null ? 1 : _adminCount.Value + 1;
    }

    public void Decrement()
    {
        _adminCount = _adminCount is null ? 0 : Math.Max(0, _adminCount.Value - 1);
    }
}
