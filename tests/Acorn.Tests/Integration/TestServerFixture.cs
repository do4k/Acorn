using System.Net;
using System.Net.Sockets;
using Acorn.Database;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Gemini;
using Acorn.Infrastructure.Telemetry;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
/// Spins up a real Acorn server with test configuration (random ports, temp SQLite DB,
/// in-memory cache) for integration testing. Shared across tests via IClassFixture.
/// </summary>
public class TestServerFixture : IAsyncLifetime
{
    private IHost? _host;
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"acorn_test_{Guid.NewGuid():N}.db");

    public int TcpPort { get; private set; }
    public int WsPort { get; private set; }

    public async Task InitializeAsync()
    {
        TcpPort = GetAvailablePort();
        WsPort = GetAvailablePort();

        // Mirror the DI registrations from Program.cs with test-safe overrides
        var configValues = new Dictionary<string, string?>
        {
            // Database — temp file-based SQLite
            ["Database:Engine"] = "SQLite",
            ["Database:ConnectionString"] = $"Data Source={_dbPath}",
            // Server / Hosting — random ports, no SLN
            ["Server:Hosting:Port"] = TcpPort.ToString(),
            ["Server:Hosting:WebSocketPort"] = WsPort.ToString(),
            ["Server:Hosting:HostName"] = "localhost",
            ["Server:Hosting:SLN:Enabled"] = "false",
            ["Server:Hosting:SLN:Url"] = "http://localhost",
            ["Server:Hosting:SLN:PingRate"] = "5",
            ["Server:Hosting:SLN:UserAgent"] = "Test",
            ["Server:Hosting:SLN:Zone"] = "Test",
            ["Server:Hosting:SLN:ServerName"] = "Test",
            ["Server:Hosting:SLN:Site"] = "http://localhost",
            ["Server:TickRate"] = "1000",
            ["Server:PlayerRecoverRate"] = "90",
            ["Server:EnforceSequence"] = "true",
            ["Server:LogPackets"] = "false",
            ["Server:NewCharacter:X"] = "6",
            ["Server:NewCharacter:Y"] = "6",
            ["Server:NewCharacter:Map"] = "1",
            // Cache — in-memory only, no Redis
            ["Cache:Enabled"] = "false",
            ["Cache:UseRedis"] = "false",
            ["Cache:ConnectionString"] = "",
            ["Cache:DefaultExpirationMinutes"] = "5",
            ["Cache:LogOperations"] = "false",
            // WiseMan / Gemini — disabled
            ["WiseManAgent:Enabled"] = "false",
            ["WiseManAgent:ApiKey"] = "",
            ["WiseManAgent:Model"] = "test",
            ["WiseManAgent:MaxResponseLength"] = "100",
            // Arena — disabled
            ["Arena:Enabled"] = "false",
            ["Arena:ArenaMapId"] = "1",
            ["Arena:SpawnInterval"] = "30",
            ["Arena:MinPlayersToBlock"] = "2",
            ["Arena:KillsToWin"] = "0",
            // Jukebox
            ["Jukebox:Cost"] = "100",
            ["Jukebox:MaxTrackId"] = "30",
            ["Jukebox:TrackTimer"] = "60",
            ["Jukebox:MaxNoteId"] = "36",
            ["Jukebox:InstrumentItems:0"] = "1",
            // Marriage
            ["Marriage:ApprovalCost"] = "1000",
            ["Marriage:DivorceCost"] = "5000",
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.Sources.Clear();
                builder.AddInMemoryCollection(configValues);
            })
            .ConfigureServices((ctx, services) =>
            {
                var cfg = ctx.Configuration;

                // Options binding (mirrors Program.cs)
                services
                    .AddSingleton<IConfiguration>(cfg)
                    .Configure<DatabaseOptions>(cfg.GetSection(DatabaseOptions.SectionName))
                    .Configure<ServerOptions>(cfg.GetSection(ServerOptions.SectionName))
                    .Configure<ArenaOptions>(cfg.GetSection(ArenaOptions.SectionName))
                    .Configure<CacheOptions>(cfg.GetSection(CacheOptions.SectionName))
                    .Configure<WiseManAgentOptions>(cfg.GetSection(WiseManAgentOptions.SectionName))
                    .Configure<JukeboxOptions>(cfg.GetSection(JukeboxOptions.SectionName))
                    .Configure<MarriageOptions>(cfg.GetSection(MarriageOptions.SectionName))
                    .AddSingleton<UtcNowDelegate>(() => DateTime.UtcNow)
                    .AddSingleton<AcornMetrics>();

                // Database — SQLite with temp file
                services.AddDbContext<AcornDbContext>((sp, options) =>
                {
                    options.UseSqlite($"Data Source={_dbPath}");
                });

                // Caching — will use InMemoryCacheService since Enabled=false
                services.AddCaching();

                // Core services (mirrors Program.cs)
                services
                    .AddSingleton<IStatsReporter, StatsReporter>()
                    .AddSingleton<ISessionGenerator, SessionGenerator>()
                    .AddSingleton<IStatCalculator, StatCalculator>()
                    .AddSingleton<IInventoryService, InventoryService>()
                    .AddSingleton<IBankService, BankService>()
                    .AddSingleton<IPaperdollService, PaperdollService>()
                    .AddSingleton<IWeightCalculator, WeightCalculator>()
                    .AddSingleton<ICharacterMapper, CharacterMapper>()
                    .AddSingleton<DropFileTextLoader>()
                    .AddSingleton<INotificationService, NotificationService>()
                    .AddScoped<IDbInitialiser, DbInitialiser>();

                // Hosted services
                services
                    .AddHostedService<DropTableHostedService>()
                    .AddHostedService<TcpListenerHostedService>()
                    .AddHostedService<WebSocketListenerHostedService>()
                    .AddHostedService<WorldHostedService>()
                    .AddHostedService<PubFileCacheHostedService>()
                    .AddHostedService<MapCacheHostedService>()
                    .AddHostedService<CharacterCacheHostedService>();

                // World state and game services
                services
                    .AddSingleton<WorldState>()
                    .AddSingleton<IWorldQueries, WorldStateQueries>()
                    .AddAllOfType<ITalkHandler>()
                    .AddAllOfType<IPlayerCommandHandler>()
                    .AddPacketHandlers()
                    .AddRepositories()
                    .AddWorldServices();

                // Networking
                services
                    .AddSingleton<WebSocketCommunicatorFactory>()
                    .AddSingleton<TcpCommunicatorFactory>()
                    .AddSingleton<MapStateFactory>()
                    .AddSingleton<PlayerStateFactory>()
                    .AddSingleton<ConnectionHandler>()
                    .AddHostedService<PlayerPingHostedService>()
                    .AddHostedService<ServerLinkNetworkPingHostedService>();

                // Refit HTTP clients (pointed at localhost — never actually called in tests)
                services
                    .AddRefitClient<IServerLinkNetworkClient>()
                    .ConfigureHttpClient((_, c) => { c.BaseAddress = new Uri("http://localhost"); });

                // WiseMan / Gemini (disabled, but DI graph still needs the types registered)
                services.AddSingleton<WiseManTalkHandler>();
                services.AddSingleton(provider =>
                    provider.GetRequiredService<IOptions<WiseManAgentOptions>>().Value);
                services
                    .AddSingleton<IWiseManAgent, WiseManGeminiAgent>()
                    .AddSingleton<WiseManQueueService>()
                    .AddHostedService(sp => sp.GetRequiredService<WiseManQueueService>())
                    .AddRefitClient<IGeminiClient>()
                    .ConfigureHttpClient(c => { c.BaseAddress = new Uri("http://localhost"); });
            })
            .ConfigureLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        // Initialize database (EnsureCreatedAsync)
        using (var scope = _host.Services.CreateScope())
        {
            var initialiser = scope.ServiceProvider.GetRequiredService<IDbInitialiser>();
            await initialiser.InitialiseAsync();
        }

        await _host.StartAsync();
        await WaitForPortReady(TcpPort);
        await WaitForPortReady(WsPort);
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // HttpListener may throw ObjectDisposedException during shutdown
            }

            _host.Dispose();
        }

        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Whether the WebSocket listener started successfully.
    /// HttpListener may fail to bind on some platforms (macOS requires elevated perms for wildcard).
    /// </summary>
    public bool IsWebSocketAvailable { get; private set; } = true;

    /// <summary>
    /// Finds an available TCP port by binding to port 0 and reading the assigned port.
    /// </summary>
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Polls a port until it accepts TCP connections, with a timeout.
    /// BackgroundService.ExecuteAsync starts asynchronously, so listeners may not
    /// be ready immediately after host.StartAsync returns.
    /// </summary>
    private async Task WaitForPortReady(int port)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(IPAddress.Loopback, port, cts.Token);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(100, cts.Token);
            }
        }

        // If this is the WS port, mark it unavailable rather than failing
        if (port == WsPort)
        {
            IsWebSocketAvailable = false;
            return;
        }

        throw new TimeoutException($"Server port {port} did not become ready within 10 seconds");
    }
}
