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
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
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
    private readonly string _mapsPath = Path.Combine(Path.GetTempPath(), $"acorn_test_maps_{Guid.NewGuid():N}");
    private readonly string _pubPath = Path.Combine(Path.GetTempPath(), $"acorn_test_pub_{Guid.NewGuid():N}");

    public int TcpPort { get; private set; }
    public int WsPort { get; private set; }

    /// <summary>
    ///     Whether the server enforces walk timestamps. Tests that exercise walk
    ///     timing override this to <c>true</c>; the default is <c>false</c> so the
    ///     shared client helper can send a fixed timestamp of 0.
    /// </summary>
    protected virtual bool EnforceTimestamps => false;

    /// <summary>
    ///     Number of players currently connected to the server's world state.
    ///     Lets tests assert that disconnects clean up world state.
    /// </summary>
    public int OnlinePlayerCount =>
        _host?.Services.GetRequiredService<WorldState>().Players.Count ?? 0;

    /// <summary>
    ///     Looks up a connected player's server-side state so tests can inspect
    ///     in-game position, client state, etc.
    /// </summary>
    public Acorn.Net.PlayerState? GetPlayer(int sessionId) =>
        _host?.Services.GetRequiredService<WorldState>().GetPlayer(sessionId);

    /// <summary>
    ///     Looks up a loaded map's server-side state so tests can inspect chests, doors, etc.
    /// </summary>
    public Acorn.World.Map.MapState? GetMap(int mapId) =>
        _host?.Services.GetRequiredService<WorldState>().MapForId(mapId);

    public async Task InitializeAsync()
    {
        TcpPort = GetAvailablePort();
        WsPort = GetAvailablePort();
        WriteTestMap(_mapsPath);
        WriteTestPubFiles(_pubPath);

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
            ["Server:EnforceTimestamps"] = EnforceTimestamps.ToString(),
            ["Server:LogPackets"] = "false",
            ["Server:NewCharacter:X"] = "6",
            ["Server:NewCharacter:Y"] = "6",
            ["Server:NewCharacter:Map"] = "1",
            // Ping cadence — short initial delay and interval so the ping tests
            // run quickly while still being slower than the login/sequence flows.
            ["Server:PlayerPingInitialDelaySeconds"] = "3",
            ["Server:PlayerPingIntervalSeconds"] = "1",
            // Map + pub data — minimal generated files so enter-game/walk can be tested
            ["Data:EcfFile"] = Path.Combine(_pubPath, "dat001.ecf"),
            ["Data:EifFile"] = Path.Combine(_pubPath, "dat001.eif"),
            ["Data:EnfFile"] = Path.Combine(_pubPath, "dtn001.enf"),
            ["Data:EsfFile"] = Path.Combine(_pubPath, "dsl001.esf"),
            ["Data:MapsPath"] = _mapsPath,
            // Cache — in-memory, disabled for tests
            ["Cache:Enabled"] = "false",
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
                    .Configure<DataOptions>(cfg.GetSection(DataOptions.SectionName))
                    .Configure<ArenaOptions>(cfg.GetSection(ArenaOptions.SectionName))
                    .Configure<CacheOptions>(cfg.GetSection(CacheOptions.SectionName))
                    .Configure<WiseManAgentOptions>(cfg.GetSection(WiseManAgentOptions.SectionName))
                    .Configure<JukeboxOptions>(cfg.GetSection(JukeboxOptions.SectionName))
                    .Configure<MarriageOptions>(cfg.GetSection(MarriageOptions.SectionName))
                    .Configure<PartyOptions>(cfg.GetSection(PartyOptions.SectionName))
                    .Configure<GuildOptions>(cfg.GetSection(GuildOptions.SectionName))
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
                    .AddSingleton<ITradeService, TradeService>()
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

            // The server's disconnect cleanup runs on a background task and uses
            // scoped services (DbContext). Wait for every player to be removed before
            // tearing down DI, otherwise the cleanup races disposal and throws
            // ObjectDisposedException/SqliteException during test-class cleanup.
            await DisconnectAndWaitForPlayersAsync();

            try
            {
                _host.Dispose();
            }
            catch (Exception)
            {
                // Best effort: a background disconnect cleanup can still be finishing.
            }
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

        try
        {
            if (Directory.Exists(_mapsPath))
            {
                Directory.Delete(_mapsPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }

        try
        {
            if (Directory.Exists(_pubPath))
            {
                Directory.Delete(_pubPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    ///     Disconnects any remaining players and waits (best effort) for the server's
    ///     background disconnect cleanup to finish before the host is disposed.
    /// </summary>
    private async Task DisconnectAndWaitForPlayersAsync()
    {
        WorldState? world;
        try
        {
            world = _host!.Services.GetService<WorldState>();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        if (world is null)
        {
            return;
        }

        foreach (var player in world.Players.Values.ToList())
        {
            player.Disconnect();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!cts.IsCancellationRequested && world.Players.Count > 0)
        {
            try
            {
                await Task.Delay(25, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Whether the WebSocket listener started successfully.
    /// HttpListener may fail to bind on some platforms (macOS requires elevated perms for wildcard).
    /// </summary>
    public bool IsWebSocketAvailable { get; private set; } = true;

    /// <summary>
    ///     Finds an available TCP port by binding to port 0 and reading the assigned port.
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
    ///     Writes a minimal, valid Endless Online map (1.emf) into <paramref name="dir"/>
    ///     so the server loads a map into world state. Real .emf files are gitignored
    ///     copyright data, so integration tests generate a small empty map instead.
    /// </summary>
    private static void WriteTestMap(string dir)
    {
        Directory.CreateDirectory(dir);

        // Map 1 is where every test character starts. It has the door tiles at
        // (5,5)/(6,5) and a warp tile at (7,8) leading to map 2, placed off the paths
        // the other integration tests walk.
        WriteMap(dir, 1, "TestMap", new List<MapWarpRow>
        {
            // Doors above the spawn point: (6,5) is unlocked, (5,5) is locked.
            new()
            {
                Y = 5,
                Tiles = new List<MapWarpRowTile>
                {
                    new()
                    {
                        X = 5,
                        Warp = new MapWarp
                        {
                            DestinationMap = 0,
                            DestinationCoords = new Coords { X = 0, Y = 0 },
                            LevelRequired = 0,
                            Door = 2
                        }
                    },
                    new()
                    {
                        X = 6,
                        Warp = new MapWarp
                        {
                            DestinationMap = 0,
                            DestinationCoords = new Coords { X = 0, Y = 0 },
                            LevelRequired = 0,
                            Door = 1
                        }
                    }
                }
            },
            new()
            {
                Y = 8,
                Tiles = new List<MapWarpRowTile>
                {
                    new()
                    {
                        X = 7,
                        Warp = new MapWarp
                        {
                            DestinationMap = 2,
                            DestinationCoords = new Coords { X = 5, Y = 5 },
                            LevelRequired = 0,
                            Door = 0
                        }
                    }
                }
            }
        });

        // Map 2 is the warp destination.
        WriteMap(dir, 2, "TestMap2", new List<MapWarpRow>());
    }

    private static void WriteMap(string dir, int id, string name, List<MapWarpRow> warpRows)
    {
        var emf = new Emf
        {
            Name = name,
            // Large enough that the 40-step sequence test stays inside the map now
            // that walk bounds are enforced.
            Width = 100,
            Height = 100,
            FillTile = 1,
            MapAvailable = true,
            CanScroll = true,
            RelogX = 2,
            RelogY = 2,
            Type = MapType.Normal,
            TimedEffect = MapTimedEffect.None,
            MusicId = 0,
            MusicControl = MapMusicControl.InterruptPlayNothing,
            AmbientSoundId = 0,
            Npcs = new List<MapNpc>(),
            Items = new List<Moffat.EndlessOnline.SDK.Protocol.Map.MapItem>(),
            TileSpecRows = id == 1
                ? new List<MapTileSpecRow>
                {
                    // A chair for sit/stand tests, placed off the paths other tests walk.
                    new()
                    {
                        Y = 11,
                        Tiles = new List<MapTileSpecRowTile>
                        {
                            new() { X = 6, TileSpec = MapTileSpec.ChairAll }
                        }
                    },
                    // A chest for map interaction tests, directly left of the spawn point.
                    new()
                    {
                        Y = 6,
                        Tiles = new List<MapTileSpecRowTile>
                        {
                            new() { X = 5, TileSpec = MapTileSpec.Chest }
                        }
                    },
                    // A second chest far from spawn, used to verify range rejection.
                    new()
                    {
                        Y = 15,
                        Tiles = new List<MapTileSpecRowTile>
                        {
                            new() { X = 15, TileSpec = MapTileSpec.Chest }
                        }
                    }
                }
                : new List<MapTileSpecRow>(),
            WarpRows = warpRows,
            GraphicLayers = Enumerable.Range(0, 9)
                .Select(_ => new MapGraphicLayer { GraphicRows = new List<MapGraphicRow>() })
                .ToList(),
            Signs = new List<MapSign>(),
            LegacyDoorKeys = new List<MapLegacyDoorKey>(),
            Rid = new List<int> { 1, 2 }
        };

        var writer = new EoWriter();
        emf.Serialize(writer);
        File.WriteAllBytes(Path.Combine(dir, $"{id}.emf"), writer.ToByteArray());
    }

    /// <summary>
    ///     Writes minimal, valid pub files (ECF/EIF/ENF/ESF) with zero records into
    ///     <paramref name="dir"/>. The WelcomeReply packet requires each RID to be a
    ///     2-element list, which empty in-memory pubs don't satisfy, so enter-game
    ///     integration tests host these tiny files and point the server at them.
    /// </summary>
    private static void WriteTestPubFiles(string dir)
    {
        Directory.CreateDirectory(dir);

        WritePub(dir, "dat001.ecf", new Ecf
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalClassesCount = 0,
            Classes = new List<EcfRecord>()
        });
        WritePub(dir, "dat001.eif", new Eif
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalItemsCount = 0,
            Items = new List<EifRecord>()
        });
        WritePub(dir, "dtn001.enf", new Enf
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalNpcsCount = 0,
            Npcs = new List<EnfRecord>()
        });
        WritePub(dir, "dsl001.esf", new Esf
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalSkillsCount = 0,
            Skills = new List<EsfRecord>()
        });
    }

    private static void WritePub(string dir, string fileName, object pub)
    {
        var writer = new EoWriter();
        pub.GetType().GetMethod("Serialize")!.Invoke(pub, [writer]);
        File.WriteAllBytes(Path.Combine(dir, fileName), writer.ToByteArray());
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
