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
    private readonly TcpListener _listener = new(IPAddress.Any, serverOptions.Value.Hosting.Port);
    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private HttpListener? _wsListener;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await statsReporter.Report();
            _listener.Start();

            // Start WebSocket listener on all interfaces
            _wsListener = new HttpListener();

            // Try wildcard binding first (works in Docker/Linux without special permissions)
            _wsListener.Prefixes.Add($"http://*:{_serverOptions.Hosting.WebSocketPort}/");

            try
            {
                _wsListener.Start();
                logger.LogInformation("Waiting for TCP on {Endpoint} and WebSocket on http://*:{Port}/ (all interfaces)...",
                    _listener.LocalEndpoint, _serverOptions.Hosting.WebSocketPort);
            }
            catch (HttpListenerException ex) when (ex.Message.Contains("Access is denied") || ex.ErrorCode == 5)
            {
                // On Windows without admin rights, try + instead of *
                logger.LogWarning(ex, "Failed to bind to *:{Port}, trying +:{Port} (requires admin on Windows)",
                    _serverOptions.Hosting.WebSocketPort, _serverOptions.Hosting.WebSocketPort);

                _wsListener.Prefixes.Clear();
                _wsListener.Prefixes.Add($"http://+:{_serverOptions.Hosting.WebSocketPort}/");

                try
                {
                    _wsListener.Start();
                    logger.LogInformation("WebSocket listener started on http://+:{Port}/",
                        _serverOptions.Hosting.WebSocketPort);
                }
                catch (HttpListenerException ex2)
                {
                    logger.LogError(ex2, "Failed to start WebSocket listener on port {Port}. " +
                        "On Windows, you may need to run as administrator or use netsh to reserve the URL. " +
                        "Command: netsh http add urlacl url=http://+:{Port}/ user=Everyone",
                        _serverOptions.Hosting.WebSocketPort, _serverOptions.Hosting.WebSocketPort);

                    // Last resort: Try localhost binding only
                    _wsListener.Prefixes.Clear();
                    _wsListener.Prefixes.Add($"http://localhost:{_serverOptions.Hosting.WebSocketPort}/");
                    _wsListener.Prefixes.Add($"http://127.0.0.1:{_serverOptions.Hosting.WebSocketPort}/");

                    try
                    {
                        _wsListener.Start();
                        logger.LogWarning("WebSocket listener started on localhost only (port {Port}). " +
                            "External connections will not work. Run as administrator for full functionality.",
                            _serverOptions.Hosting.WebSocketPort);
                    }
                    catch (Exception fallbackEx)
                    {
                        logger.LogError(fallbackEx, "Failed to start WebSocket listener even on localhost");
                        throw;
                    }
                }
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpAcceptTask = _listener.AcceptTcpClientAsync(cancellationToken).AsTask();
                    var wsAcceptTask = _wsListener.GetContextAsync();
                    var completed = await Task.WhenAny(tcpAcceptTask, wsAcceptTask);

                    // Check if cancellation was requested
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    ICommunicator communicator = completed switch
                    {
                        Task<TcpClient> tcp when tcp == tcpAcceptTask =>
                            tcpCommunicatorFactory.Initialise(tcp.Result),
                        Task<HttpListenerContext> ws when ws == wsAcceptTask =>
                            await HandleWebSocketConnection(ws.Result, cancellationToken),
                        _ => throw new InvalidOperationException("Unexpected task completion")
                    };

                    var sessionId = sessionGenerator.Generate();

                    var playerState = playerStateFactory.CreatePlayerState(communicator, sessionId,
                        async player => await OnClientDisposed(player, sessionId));

                    var added = worldState.Players.TryAdd(sessionId, playerState);
                    logger.LogInformation("Connection accepted. {PlayersConnected} players connected",
                        worldState.Players.Count);
                    UpdateConnectedCount();
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    logger.LogDebug("Accept operation cancelled during shutdown");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // HttpListener was disposed during shutdown, this is expected
                    logger.LogDebug("Listener disposed during shutdown");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Listener service cancellation requested");
        }
        catch (ObjectDisposedException)
        {
            logger.LogDebug("Listener service resources disposed during shutdown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in connection listener");
            throw;
        }
        finally
        {
            logger.LogInformation("Connection listener stopped");
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

    private async Task<ICommunicator> HandleWebSocketConnection(HttpListenerContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("WebSocket connection attempt from {RemoteEndpoint}", context.Request.RemoteEndPoint);

        if (!context.Request.IsWebSocketRequest)
        {
            logger.LogWarning("Received non-WebSocket HTTP request to WebSocket endpoint from {RemoteEndpoint}",
                context.Request.RemoteEndPoint);
            context.Response.StatusCode = 400;
            context.Response.Close();
            throw new InvalidOperationException("Not a WebSocket request");
        }

        logger.LogInformation("Accepting WebSocket connection from {RemoteEndpoint}", context.Request.RemoteEndPoint);
        return await webSocketCommunicatorFactory.InitialiseAsync(context, cancellationToken);
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