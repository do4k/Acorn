using System.Net;
using System.Net.Sockets;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.Options;
using Acorn.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

public class NewConnectionHostedService(
    ILogger<NewConnectionHostedService> logger,
    IStatsReporter statsReporter,
    WorldState worldState,
    IDbRepository<Character> characterRepository,
    ICharacterMapper characterMapper,
    ISessionGenerator sessionGenerator,
    IOptions<ServerOptions> serverOptions,
    TcpCommunicatorFactory tcpCommunicatorFactory,
    WebSocketCommunicatorFactory webSocketCommunicatorFactory,
    PlayerStateFactory playerStateFactory
) : BackgroundService
{
    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private readonly TcpListener _listener = new(IPAddress.Any, serverOptions.Value.Hosting.Port);
    private HttpListener? _wsListener;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await statsReporter.Report();
        _listener.Start();

        // Start WebSocket listener
        // Note: Using localhost instead of * to avoid requiring admin privileges on Windows
        // Change to http://+: or http://*: if you need external access and have reserved the URL
        _wsListener = new HttpListener();
        _wsListener.Prefixes.Add($"http://localhost:{_serverOptions.Hosting.WebSocketPort}/");
        _wsListener.Start();

        logger.LogInformation("Waiting for TCP on {Endpoint} and WebSocket on ws://localhost:{Port}...",
            _listener.LocalEndpoint, _serverOptions.Hosting.WebSocketPort);

        while (!cancellationToken.IsCancellationRequested)
        {
            var tcpAcceptTask = _listener.AcceptTcpClientAsync(cancellationToken).AsTask();
            var wsAcceptTask = _wsListener.GetContextAsync();
            var completed = await Task.WhenAny(tcpAcceptTask, wsAcceptTask);
            ICommunicator communicator = completed switch
            {
                Task<TcpClient> tcp when tcp == tcpAcceptTask => tcpCommunicatorFactory.Initialise(tcp.Result),
                Task<HttpListenerContext> ws when ws == wsAcceptTask => await webSocketCommunicatorFactory.InitialiseAsync(ws.Result, cancellationToken),
                _ => throw new InvalidOperationException("Unexpected task completion")
            };

            var sessionId = sessionGenerator.Generate();

            var playerState = playerStateFactory.CreatePlayerState(communicator, sessionId,
                async (player) => await OnClientDisposed(player, sessionId));

            var added = worldState.Players.TryAdd(sessionId, playerState);
            logger.LogInformation("Connection accepted. {PlayersConnected} players connected", worldState.Players.Count);
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
        Console.Title = $"Acorn Server ({worldState.Players.Count} Connected)";
    }

    private async Task OnClientDisposed(PlayerState player, int sessionId)
    {
        if (player.Character is not null && player.CurrentMap is not null)
        {
            await player.CurrentMap.NotifyLeave(player);
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character));
        }

        worldState.Players.TryRemove(sessionId, out _);
        logger.LogInformation("Player disconnected");
        UpdateConnectedCount();
    }

    public override void Dispose()
    {
        _listener.Stop();
        _listener.Dispose();
        
        if (_wsListener != null)
        {
            _wsListener.Stop();
            _wsListener.Close();
        }
        
        base.Dispose();
    }
}