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
    ILogger<NewConnectionHostedService> logger,
    IStatsReporter statsReporter,
    WorldState worldState,
    IDbRepository<Character> characterRepository,
    ISessionGenerator sessionGenerator,
    IOptions<ServerOptions> serverOptions,
    TcpCommunicatorFactory tcpCommunicatorFactory,
    WebSocketCommunicatorFactory webSocketCommunicatorFactory,
    PlayerStateFactory playerStateFactory
) : BackgroundService
{
    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private readonly TcpListener _listener = new(IPAddress.Any, serverOptions.Value.Hosting.Port);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await statsReporter.Report();
        _listener.Start();

        // Start WebSocket listener
        var wsListener = new HttpListener();
        wsListener.Prefixes.Add($"http://*:{_serverOptions.Hosting.WebSocketPort}/");
        wsListener.Start();

        logger.LogInformation("Waiting for TCP on {Endpoint} and WebSocket on ws://*:{Port}...",
            _listener.LocalEndpoint, _serverOptions.Hosting.WebSocketPort);

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
            await characterRepository.UpdateAsync(player.Character);
        }

        worldState.Players.TryRemove(sessionId, out _);
        logger.LogInformation("Player disconnected");
        UpdateConnectedCount();
    }

    public override void Dispose()
    {
        _listener.Stop();
        _listener.Dispose();
        base.Dispose();
    }
}