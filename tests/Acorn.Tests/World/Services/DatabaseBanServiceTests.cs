using Acorn.Database;
using Acorn.World.Services.Bans;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acorn.Tests.World.Services;

public class DatabaseBanServiceTests
{
    private static ServiceProvider CreateProvider()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<AcornDbContext>(options => options.UseSqlite(connection));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AcornDbContext>().Database.EnsureCreated();

        return provider;
    }

    private static DatabaseBanService CreateSut(IServiceScopeFactory scopeFactory)
        => new(scopeFactory, NullLogger<DatabaseBanService>.Instance);

    [Test]
    public void Ban_ShouldPersistAndReloadOnNextStart()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        CreateSut(scopeFactory).Ban(BanKeys.Username("Cheater"));

        var reloaded = CreateSut(scopeFactory);
        reloaded.IsBanned(BanKeys.Username("cheater")).Should().BeTrue();
    }

    [Test]
    public void Unban_ShouldRemovePersistedBan()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var sut = CreateSut(scopeFactory);
        sut.Ban(BanKeys.Hdid("abc"));
        sut.Unban(BanKeys.Hdid("abc")).Should().BeTrue();

        CreateSut(scopeFactory).IsBanned(BanKeys.Hdid("abc")).Should().BeFalse();
    }

    [Test]
    public void IsBanned_WhenExpired_ShouldReturnFalse()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var sut = CreateSut(scopeFactory);
        sut.Ban(BanKeys.Ip("127.0.0.1"), expiresAt: DateTime.UtcNow.AddSeconds(-1));

        sut.IsBanned(BanKeys.Ip("127.0.0.1")).Should().BeFalse();
    }
}
