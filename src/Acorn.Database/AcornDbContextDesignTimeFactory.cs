using Acorn.Shared.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acorn.Database;

/// <summary>
///     Creates an <see cref="AcornDbContext" /> for the EF Core command line tools
///     (<c>dotnet ef</c>). The provider and connection string come from the same
///     <c>Database__Engine</c> / <c>Database__ConnectionString</c> environment variables the
///     server uses, defaulting to SQLite so migrations can be generated and applied for the
///     development database.
/// </summary>
public class AcornDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AcornDbContext>
{
    public AcornDbContext CreateDbContext(string[] args)
    {
        var engine = Environment.GetEnvironmentVariable("Database__Engine") ?? "sqlite";
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString")
                               ?? "Data Source=Acorn.db";

        var optionsBuilder = new DbContextOptionsBuilder<AcornDbContext>();
        optionsBuilder.UseDatabaseEngine(engine, connectionString);

        return new AcornDbContext(optionsBuilder.Options);
    }
}
