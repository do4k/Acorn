using Microsoft.EntityFrameworkCore;

namespace Acorn.Shared.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Configures the database provider on the given <see cref="DbContextOptionsBuilder"/>
    /// based on the engine name (postgresql, mysql, sqlserver, sqlite, etc.).
    /// </summary>
    public static DbContextOptionsBuilder UseDatabaseEngine(
        this DbContextOptionsBuilder options, string? engine, string? connectionString)
    {
        switch (engine?.ToLower() ?? "sqlite")
        {
            case "postgresql":
            case "postgres":
                options.UseNpgsql(connectionString);
                break;
            case "mysql":
            case "mariadb":
                options.UseMySQL(connectionString!);
                break;
            case "sqlserver":
            case "mssql":
                options.UseSqlServer(connectionString);
                break;
            case "sqlite":
            default:
                options.UseSqlite(connectionString);
                break;
        }

        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();

        return options;
    }
}
