using System.Reflection;
using Acorn.Database;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Gemini;
using Acorn.Infrastructure.Logging;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Options;
using Acorn.Shared.Options;
using Acorn.SLN;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Refit;

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
            .Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName))
            .Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName))
            .Configure<ArenaOptions>(configuration.GetSection(ArenaOptions.SectionName))
            .Configure<WiseManAgentOptions>(configuration.GetSection(WiseManAgentOptions.SectionName))
            .Configure<JukeboxOptions>(configuration.GetSection(JukeboxOptions.SectionName))
            .Configure<MarriageOptions>(configuration.GetSection(MarriageOptions.SectionName))
            .Configure<GuildOptions>(configuration.GetSection(GuildOptions.SectionName))
            .AddSingleton<UtcNowDelegate>(() => DateTime.UtcNow)
            .AddSingleton<AcornMetrics>()
            // Database + caching infrastructure: options binding, DbContext and in-memory cache
            .AddAcornDataInfrastructure(configuration);

        // Configure OpenTelemetry metrics, traces and logging export via OTLP
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("acorn"))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(AcornMetrics.MeterName);
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(AcornMetrics.MeterName);
            })
            .UseOtlpExporter();

        services
            .AddSingleton<IStatsReporter, StatsReporter>()
            .AddSingleton<ISessionGenerator, SessionGenerator>()
            // Game services
            .AddSingleton<IStatCalculator, StatCalculator>()
            .AddSingleton<IInventoryService, InventoryService>()
            .AddSingleton<IBankService, BankService>()
            .AddSingleton<IPaperdollService, PaperdollService>()
            .AddSingleton<IWeightCalculator, WeightCalculator>()
            .AddSingleton<ICharacterMapper, CharacterMapper>()
            .AddSingleton<DropFileTextLoader>()
            // Notification services
            .AddSingleton<INotificationService, NotificationService>()
            .AddScoped<IDbInitialiser, DbInitialiser>()
            .AddHostedService<DropTableHostedService>()
            .AddHostedService<TcpListenerHostedService>()
            .AddHostedService<WebSocketListenerHostedService>()
            .AddHostedService<WorldHostedService>()
            .AddHostedService<PubFileCacheHostedService>()
            .AddHostedService<MapCacheHostedService>()
            .AddHostedService<CharacterCacheHostedService>()
            .AddSingleton<WorldState>()
            .AddSingleton<IWorldQueries, WorldStateQueries>()
            .AddAllOfType<ITalkHandler>()
            .AddAllOfType<IPlayerCommandHandler>()
            .AddPacketHandlers()
            .AddRepositories()
            .AddWorldServices()
            .AddSingleton<WebSocketCommunicatorFactory>()
            .AddSingleton<TcpCommunicatorFactory>()
            .AddSingleton<MapStateFactory>()
            .AddSingleton<PlayerStateFactory>()
            .AddSingleton<ConnectionHandler>()
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

        // File fallback so logs are always tailable even if the Aspire
        // dashboard resource-log view is unavailable.
        var logPath = configuration["Logging:File:Path"] ?? "acorn.log";
        var logLevel = Enum.TryParse<LogLevel>(configuration["Logging:File:MinimumLevel"], true, out var fileMin)
            ? fileMin
            : LogLevel.Information;
        builder.AddFileLogger(logPath, logLevel);
    })
    .Build();

// Initialize database
using (var scope = host.Services.CreateScope())
{
    var initialiser = scope.ServiceProvider.GetRequiredService<IDbInitialiser>();
    await initialiser.InitialiseAsync();
}

await host.RunAsync();