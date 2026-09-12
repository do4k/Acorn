using Acorn.Database;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Acorn.Tests.Database;

/// <summary>
///     The server schema is managed by EF Core migrations, applied as a startup step
///     (scripts/run-apphost.sh). These tests guard the migration set against model drift
///     and verify it creates a usable schema from an empty database.
/// </summary>
public class MigrationsTests
{
    [Test]
    public void Model_ShouldNotHavePendingChanges()
    {
        using var context = CreateContext("Data Source=:memory:");

        context.Database.HasPendingModelChanges().Should().BeFalse(
            "the model changed without a migration - run 'dotnet ef migrations add <name>'");
    }

    [Test]
    public async Task MigrateAsync_WhenDatabaseIsEmpty_ShouldCreateTheSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"acorn_migrate_{Guid.NewGuid():N}.db");
        try
        {
            await using var context = CreateContext($"Data Source={path}");

            await context.Database.MigrateAsync();

            (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
            (await context.Accounts.CountAsync()).Should().Be(0);
            (await context.Characters.CountAsync()).Should().Be(0);
            (await context.BoardPosts.CountAsync()).Should().Be(0);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static AcornDbContext CreateContext(string connectionString)
    {
        return new AcornDbContext(new DbContextOptionsBuilder<AcornDbContext>()
            .UseSqlite(connectionString)
            .Options);
    }
}
