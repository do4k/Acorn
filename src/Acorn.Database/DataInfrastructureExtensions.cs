using System.Text.RegularExpressions;
using Acorn.Shared.Extensions;
using Acorn.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Database;

/// <summary>
///     Registers the shared data infrastructure (options, EF Core DbContext and caching)
///     used by both the game server and the REST API.
/// </summary>
public static class DataInfrastructureExtensions
{
    /// <summary>
    ///     Binds <see cref="DatabaseOptions"/> and <see cref="CacheOptions"/>, registers the
    ///     <see cref="AcornDbContext"/> using the configured engine, and adds in-memory caching.
    /// </summary>
    public static IServiceCollection AddAcornDataInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName))
            .Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName))
            .AddCaching();

        services.AddDbContext<AcornDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var connectionString = dbOptions.ConnectionString;
            var dbEngine = dbOptions.Engine?.ToLower() ?? "sqlite";

            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
            logger.LogInformation(
                "Configuring database context - Engine: {Engine}, ConnectionString: {ConnectionString}",
                dbEngine, MaskConnectionString(connectionString));
            logger.LogInformation("Using {Engine} database provider", dbEngine);

            options.UseDatabaseEngine(dbEngine, connectionString);
        });

        return services;
    }

    /// <summary>
    ///     Masks sensitive information (e.g. passwords) in a connection string for safe logging.
    /// </summary>
    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return "[empty]";
        }

        if (connectionString.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("pwd", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.Replace(
                connectionString,
                @"Password\s*=\s*[^;]*",
                "Password=***",
                RegexOptions.IgnoreCase);
        }

        return connectionString;
    }
}
