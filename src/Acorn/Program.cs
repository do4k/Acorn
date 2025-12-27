using System.Reflection;
using Acorn.Database;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Caching;
using Acorn.Infrastructure.Communicators;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Options;
using Acorn.SLN;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
using StackExchange.Redis;

var GREEN = Console.IsOutputRedirected ? "" : "\x1b[92m";
var NORMAL = Console.IsOutputRedirected ? "" : "\x1b[39m";
var BOLD = Console.IsOutputRedirected ? "" : "\x1b[1m";
var NOBOLD = Console.IsOutputRedirected ? "" : "\x1b[22m";

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

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true)
    .AddJsonFile("appsettings.MySql.json", true, true)
    .AddJsonFile("appsettings.Postgres.json", true, true)
    .AddJsonFile("appsettings.SqlServer.json", true, true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
    .Build();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .AddSingleton<IConfiguration>(configuration)
            .Configure<DatabaseOptions>(configuration.GetSection("Database"))
            .Configure<ServerOptions>(configuration.GetSection("Server"))
            .Configure<CacheOptions>(configuration.GetSection("Cache"))
            .AddSingleton<UtcNowDelegate>(() => DateTime.UtcNow);

        // Configure DbContext based on database engine
        services.AddDbContext<AcornDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var connectionString = dbOptions.ConnectionString;
            var engine = dbOptions.Engine?.ToLower() ?? "sqlite";

            switch (engine)
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

            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
        });

        // Configure Caching (Redis or In-Memory)
        services.AddSingleton<ICacheService>(sp =>
        {
            var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
            
            if (!cacheOptions.Enabled)
            {
                logger.LogInformation("Caching is disabled");
                return new InMemoryCacheService();
            }
            
            if (cacheOptions.UseRedis)
            {
                try
                {
                    var redis = ConnectionMultiplexer.Connect(cacheOptions.ConnectionString);
                    logger.LogInformation("Connected to Redis at {ConnectionString}", cacheOptions.ConnectionString);
                    return new RedisCacheService(redis);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to connect to Redis, falling back to in-memory cache");
                    return new InMemoryCacheService();
                }
            }
            
            logger.LogInformation("Using in-memory cache");
            return new InMemoryCacheService();
        });

        services
            .AddSingleton<IStatsReporter, StatsReporter>()
            .AddSingleton<ISessionGenerator, SessionGenerator>()
            // Game services
            .AddSingleton<IStatCalculator, StatCalculator>()
            .AddSingleton<IInventoryService, InventoryService>()
            .AddSingleton<IBankService, BankService>()
            .AddSingleton<IWeightCalculator, WeightCalculator>()
            .AddSingleton<ICharacterMapper, CharacterMapper>()
            // World services
            .AddSingleton<IMapItemService, MapItemService>()
            // Notification services
            .AddSingleton<INotificationService, NotificationService>()
            .AddScoped<IDbInitialiser, DbInitialiser>()
            .AddHostedService<NewConnectionHostedService>()
            .AddHostedService<WorldHostedService>()
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
            .AddHostedService<ServerLinkNetworkPingHostedService>()
            .AddRefitClient<IServerLinkNetworkClient>()
            .ConfigureHttpClient((svc, c) =>
            {
                var slnOptions = svc.GetRequiredService<IOptions<ServerOptions>>().Value.Hosting.SLN;
                c.BaseAddress = new Uri(slnOptions.Url);
                c.DefaultRequestHeaders.Add("User-Agent", slnOptions.UserAgent);
            });
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
