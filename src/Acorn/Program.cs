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
using Acorn.Net.PacketHandlers.Player.Talk.Acornbot;
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
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

// Bootstrap configuration used only to resolve the selected database engine.
// Environment variables are included so Database__Engine can choose the
// provider-specific config file below.
var bootstrapConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true)
    .AddEnvironmentVariables()
    .Build();

var engine = bootstrapConfig["Database:Engine"] ?? "sqlite";

// The provider-specific file is layered *below* environment variables so that
// deployment overrides such as Database__ConnectionString take precedence over
// appsettings.{Engine}.json (e.g. in Docker/PaaS where the host name differs).
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true)
    .AddJsonFile($"appsettings.{engine}.json", true, true)
    .AddEnvironmentVariables();

var configuration = config
    .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
    .Build();

Console.WriteLine($"{GREEN}Database Engine:{NORMAL} {engine.ToUpper()}");

var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
var sampleRatio = configuration.GetValue("Telemetry:SampleRatio", 1.0);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services
            .AddSingleton<IConfiguration>(configuration)
            .Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName))
            .Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName))
            .Configure<ArenaOptions>(configuration.GetSection(ArenaOptions.SectionName))
            .Configure<WiseManAgentOptions>(configuration.GetSection(WiseManAgentOptions.SectionName))
            .Configure<JukeboxOptions>(configuration.GetSection(JukeboxOptions.SectionName))
            .Configure<MarriageOptions>(configuration.GetSection(MarriageOptions.SectionName))
            .Configure<PartyOptions>(configuration.GetSection(PartyOptions.SectionName))
            .Configure<GuildOptions>(configuration.GetSection(GuildOptions.SectionName))
            .Configure<AcornbotOptions>(configuration.GetSection(AcornbotOptions.SectionName))
            .Configure<BannedTextOptions>(configuration.GetSection(BannedTextOptions.SectionName))
            .AddSingleton<UtcNowDelegate>(() => DateTime.UtcNow)
            .AddSingleton<AcornMetrics>()
            // Database + caching infrastructure: options binding, DbContext and in-memory cache
            .AddAcornDataInfrastructure(configuration);

        // Configure OpenTelemetry metrics, traces and logging export via OTLP.
        // The exporter endpoint comes from OTEL_EXPORTER_OTLP_ENDPOINT (or
        // OTEL_EXPORTER_OTLP_TRACES_ENDPOINT / _METRICS_ENDPOINT / _LOGS_ENDPOINT).
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("acorn", serviceVersion: serviceVersion,
                    serviceInstanceId: $"{Environment.MachineName}:{Environment.ProcessId}")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = context.HostingEnvironment.EnvironmentName,
                    ["host.name"] = Environment.MachineName
                }))
            .WithMetrics(metrics => metrics
                .AddMeter(AcornMetrics.MeterName)
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(AcornActivitySource.Name)
                .AddEntityFrameworkCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRatio))))
            .WithLogging(
                _ => { },
                logging =>
                {
                    // Correlate structured logs with the active trace/span.
                    logging.IncludeScopes = true;
                    logging.IncludeFormattedMessage = true;
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
            .AddSingleton<ITradeService, TradeService>()
            .AddSingleton<ICharacterMapper, CharacterMapper>()
            .AddSingleton<DropFileTextLoader>()
            // Notification services
            .AddSingleton<INotificationService, NotificationService>()
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
            .AddAllOfType<IAcornbotCommand>()
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

        // Acornbot: PM-driven self-service commands. Transient so each connection
        // scope gets command handlers with that scope's repositories.
        services
            .AddTransient<IAcornbotReplyChannel, AcornbotReplyChannel>()
            .AddTransient<IAcornbotService, AcornbotService>();

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

await host.RunAsync();