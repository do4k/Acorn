using System.Net;
using System.Net.Sockets;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.Options;
using Acorn.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

public class NewConnectionHostedService(
    IServiceProvider services,
    ILogger<NewConnectionHostedService> logger,
    ILogger<PlayerState> playerConnectionLogger,
    IStatsReporter statsReporter,
    WorldState worldState,
    IDbRepository<Character> characterRepository,
    ISessionGenerator sessionGenerator,
    IOptions<ServerOptions> serverOptions,
    TcpCommunicatorFactory tcpCommunicatorFactory,
    WebSocketCommunicatorFactory webSocketCommunicatorFactory
) : BackgroundService, IDisposable
{
    private readonly IDbRepository<Character> _characterRepository = characterRepository;
    private readonly TcpListener _listener = new(IPAddress.Any, serverOptions.Value.Port);
    private readonly ILogger<NewConnectionHostedService> _logger = logger;
    private readonly ILogger<PlayerState> _playerConnectionLogger = playerConnectionLogger;
    private readonly IServiceProvider _services = services;
    private readonly IStatsReporter _statsReporter = statsReporter;
    private readonly WorldState _world = worldState;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _statsReporter.Report();
        _listener.Start();

        // Start WebSocket listener
        var wsListener = new HttpListener();
        wsListener.Prefixes.Add($"http://*:{serverOptions.Value.WebSocketPort}/");
        wsListener.Start();

        _logger.LogInformation("Waiting for TCP on {Endpoint} and WebSocket on ws://*:{Port}...",
            _listener.LocalEndpoint, serverOptions.Value.WebSocketPort);

        while (!cancellationToken.IsCancellationRequested)
        {
            var tcpAcceptTask = _listener.AcceptTcpClientAsync(cancellationToken).AsTask();
            var wsAcceptTask = wsListener.GetContextAsync();
            var completed = await Task.WhenAny(tcpAcceptTask, wsAcceptTask);
            ICommunicator communicator = completed switch
            {
                Task<TcpClient> tcp when tcp == tcpAcceptTask => tcpCommunicatorFactory.Initialise(tcp.Result),
                Task<HttpListenerContext> ws when ws == wsAcceptTask => webSocketCommunicatorFactory.Initialise(ws.Result),
                _ => throw new InvalidOperationException("Unexpected task completion")
            };
            
            var sessionId = sessionGenerator.Generate();

            var playerState = new PlayerState(_services, communicator, _playerConnectionLogger, sessionId,
                async (player) =>
                {
                    if (player.Character is not null && player.CurrentMap is not null)
                    {
                        await player.CurrentMap.NotifyLeave(player);
                        await _characterRepository.UpdateAsync(player.Character);
                    }

                    _world.Players.TryRemove(sessionId, out _);
                    _logger.LogInformation("Player disconnected");
                    UpdateConnectedCount();
                });

            var added = _world.Players.TryAdd(sessionId, playerState);
            _logger.LogInformation("Connection accepted. {PlayersConnected} players connected", _world.Players.Count);
            UpdateConnectedCount();
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return base.StopAsync(cancellationToken);
    }

    private void UpdateConnectedCount()
    {
        Console.Title = $"Acorn Server ({_world.Players.Count} Connected)";
    }

    public override void Dispose()
    {
        _listener.Stop();
        _listener.Dispose();
        base.Dispose();
    }
}