using System.Reflection;
using System.Text.RegularExpressions;
using Acorn.Database;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Gemini;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Options;
using Acorn.Shared.Extensions;
using Acorn.Shared.Options;
using Acorn.SLN;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services;
using Acorn.World.Services.Map;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

var GREEN = Console.IsOutputRedirected ? "" : "\x1b[92m";
var NORMAL = Console.IsOutputRedirected ? "" : "\x1b[39m";
var BOLD = Console.IsOutputRedirected ? "" : "\x1b[1m";
var NOBOLD = Console.IsOutputRedirected ? "" : "\x1b[22m";

// Helper function to mask sensitive connection string information
static string MaskConnectionString(string? connectionString)
{
    if (string.IsNullOrEmpty(connectionString))
    {
        return "[empty]";
    }

    // For simple display, just show if it contains a password and mask the password value
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

Console.WriteLine($"""
                   {GREEN}          _       {BOLD}Acorn Endless-Online Server Software{NOBOLD}
                           _/-\_     ------------------------------------
                       .-`-:-:-`-.   {GREEN}Author:{NORMAL} Dan Oak{GREEN}
                       /-:-:-:-:-:-\ {GREEN}Version:{NORMAL} 0.0.0.1{GREEN}
                       \:-:-:-:-:-:/ 
                        |`   ,   `|
                        |   (     |
                        `\   `   /'
                          `-._.-'    {NORMAL}
                   """);

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true)
    .AddEnvironmentVariables();

var engine = config.Build()["Database:Engine"] ?? "sqlite";

if (!string.IsNullOrWhiteSpace(engine))
{
    config.AddJsonFile($"appsettings.{engine}.json", true, true);
}

var configuration = config
    .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
    .Build();

Console.WriteLine($"{GREEN}Database Engine:{NORMAL} {engine.ToUpper()}");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .AddSingleton<IConfiguration>(configuration)
            .Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName))
            .Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName))
            .Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName))
            .Configure<WiseManAgentOptions>(configuration.GetSection(WiseManAgentOptions.SectionName))
            .Configure<ArenaOptions>(configuration.GetSection("Arena"))
            .AddSingleton<UtcNowDelegate>(() => DateTime.UtcNow);

        // Configure DbContext based on database engine
        services.AddDbContext<AcornDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var connectionString = dbOptions.ConnectionString;
            var dbEngine = dbOptions.Engine?.ToLower() ?? "sqlite";
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

            logger.LogInformation(
                "Configuring database context - Engine: {Engine}, ConnectionString: {ConnectionString}",
                dbEngine, MaskConnectionString(connectionString));

            switch (dbEngine)
            {
                case "postgresql":
                case "postgres":
                    logger.LogInformation("Using PostgreSQL database provider");
                    options.UseNpgsql(connectionString);
                    break;
                case "mysql":
                case "mariadb":
                    logger.LogInformation("Using MySQL database provider");
                    options.UseMySQL(connectionString!);
                    break;
                case "sqlserver":
                case "mssql":
                    logger.LogInformation("Using SQL Server database provider");
                    options.UseSqlServer(connectionString);
                    break;
                case "sqlite":
                default:
                    logger.LogInformation("Using SQLite database provider");
                    options.UseSqlite(connectionString);
                    break;
            }

            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        // Configure Caching (Redis or In-Memory)
        services.AddCaching();

        services
            .AddSingleton<IStatsReporter, StatsReporter>()
            .AddSingleton<ISessionGenerator, SessionGenerator>()
            // Game services
            .AddSingleton<IStatCalculator, StatCalculator>()
            .AddSingleton<ILootService, LootService>()
            .AddSingleton<IInventoryService, InventoryService>()
            .AddSingleton<IBankService, BankService>()
            .AddSingleton<IPaperdollService, PaperdollService>()
            .AddSingleton<IWeightCalculator, WeightCalculator>()
            .AddSingleton<ICharacterMapper, CharacterMapper>()
            .AddSingleton<DropFileTextLoader>()
            // World services
            .AddSingleton<IMapItemService, MapItemService>()
            // Notification services
            .AddSingleton<INotificationService, NotificationService>()
            .AddScoped<IDbInitialiser, DbInitialiser>()
            .AddHostedService<DropTableHostedService>()
            .AddHostedService<NewConnectionHostedService>()
            .AddHostedService<WorldHostedService>()
            .AddHostedService<PubFileCacheHostedService>()
            .AddHostedService<MapCacheHostedService>()
            .AddHostedService<CharacterCacheHostedService>()
            .AddSingleton<WorldState>()
            .AddSingleton<IWorldQueries, WorldStateQueries>()
            .AddAllOfType<ITalkHandler>()
            .AddPacketHandlers()
            .AddRepositories()
            .AddWorldServices()
            .AddSingleton<WebSocketCommunicatorFactory>()
            .AddSingleton<TcpCommunicatorFactory>()
            .AddSingleton<MapStateFactory>()
            .AddSingleton<PlayerStateFactory>()
            .AddHostedService<PlayerPingHostedService>()
            .AddHostedService<ServerLinkNetworkPingHostedService>()
            .AddRefitClient<IServerLinkNetworkClient>()
            .ConfigureHttpClient((svc, c) =>
            {
                var slnOptions = svc.GetRequiredService<IOptions<ServerOptions>>().Value.Hosting.SLN;
                c.BaseAddress = new Uri(slnOptions.Url);
                c.DefaultRequestHeaders.Add("User-Agent", slnOptions.UserAgent);
            });

        // Always register WiseManTalkHandler so it is available for DI, regardless of Gemini/WiseMan feature flag
        services.AddSingleton<WiseManTalkHandler>();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<WiseManAgentOptions>>().Value);

        services
            .AddSingleton<IWiseManAgent, WiseManGeminiAgent>()
            .AddSingleton<WiseManQueueService>()
            .AddHostedService(sp => sp.GetRequiredService<WiseManQueueService>())
            .AddRefitClient<IGeminiClient>()
            .ConfigureHttpClient(c => { c.BaseAddress = new Uri("https://generativelanguage.googleapis.com"); });
    })
    .ConfigureLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
#pragma warning disable CS0618
        builder.AddConsole(options => { options.TimestampFormat = "[HH:mm:ss] "; });
#pragma warning restore CS0618
    })
    .Build();

// Initialize database
using (var scope = host.Services.CreateScope())
{
    var initialiser = scope.ServiceProvider.GetRequiredService<IDbInitialiser>();
    await initialiser.InitialiseAsync();
}

await host.RunAsync();