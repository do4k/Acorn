using Acorn.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acorn.Database.PostgreSql;

/// <summary>
///     Design-time factory used by the EF Core tools (<c>dotnet ef</c>) to create an
///     <see cref="AcornDbContext" /> targeting PostgreSQL. Migrations for PostgreSQL live in
///     this assembly and are selected at runtime via <c>MigrationsAssembly</c>.
/// </summary>
public class PostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AcornDbContext>
{
    public AcornDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString")
                               ?? "Host=localhost;Port=5432;Database=acorn;Username=acorn;Password=acornpassword";

        var optionsBuilder = new DbContextOptionsBuilder<AcornDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly("Acorn.Database.PostgreSql"));

        return new AcornDbContext(optionsBuilder.Options);
    }
}
