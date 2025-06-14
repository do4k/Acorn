using System.Data;
using System.Net.Http.Headers;
using System.Reflection;
using Acorn;
using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Infrastructure;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Services;
using Acorn.SLN;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
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

Console.WriteLine($"""
{GREEN}          _       {BOLD}Acorn Endless-Online Server Software{NOBOLD}
        _/-\_     ------------------------------------
    .-`-:-:-`-.   {GREEN}Author:{NORMAL} Dan Oak{GREEN}
    /-:-:-:-:-:-\ {GREEN}Version:{NORMAL} 0.0.0.1{GREEN}
    \:-:-:-:-:-:/ 
     |`       `|
     |         |
     `\       /'
       `-._.-'    {NORMAL}
""");

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", false, true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
    .Build();

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .AddSingleton<IConfiguration>(configuration)
            .Configure<DatabaseOptions>(configuration.GetSection("Database"))
            .Configure<ServerOptions>(configuration.GetSection("Server"))
            .Configure<SLNOptions>(configuration.GetSection("SLN"))
            .AddSingleton<UtcNowDelegate>(() => DateTime.UtcNow)
            .AddTransient<IDbConnection>(sp =>
            {
                var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
                var connectionString = dbOptions.ConnectionString;
                
                if (dbOptions.Engine?.ToLower() != "sqlite")
                {
                    return new SqlConnection(connectionString);
                }
                return new SqliteConnection(connectionString);
            })
            .AddSingleton<IStatsReporter, StatsReporter>()
            .AddSingleton<ISessionGenerator, SessionGenerator>()
            .AddTransient<IDbInitialiser, DbInitialiser>()
            .AddHostedService<NewConnectionHostedService>()
            .AddHostedService<WorldHostedService>()
            .AddSingleton<WorldState>()
            .AddAllOfType(typeof(IPacketHandler<>))
            .AddAllOfType<ITalkHandler>()
            .AddRepositories();

        var slnOptions = services.BuildServiceProvider().GetService<IOptions<SLNOptions>>();
        if (slnOptions?.Value.Enabled ?? false)
        {
            services
            .AddHostedService<ServerLinkNetworkPingHostedService>()
            .AddRefitClient<IServerLinkNetworkClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(slnOptions.Value.Url);
                c.DefaultRequestHeaders.Add("User-Agent", slnOptions.Value.UserAgent);
            });
        }
    })
    .ConfigureLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddConsole(options => { options.TimestampFormat = "[HH:mm:ss] "; });
    })
    .Build()
    .RunAsync();