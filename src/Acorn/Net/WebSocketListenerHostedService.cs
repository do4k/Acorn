using System.Net;
using Acorn.Infrastructure.Communicators;
using Acorn.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

/// <summary>
/// Listens for incoming WebSocket connections via HttpListener on the configured port.
/// Each accepted connection is handed off to <see cref="ConnectionHandler"/>.
/// Operates independently from <see cref="TcpListenerHostedService"/> so that
/// neither transport blocks or drops connections from the other.
/// </summary>
public class WebSocketListenerHostedService(
    ILogger<WebSocketListenerHostedService> logger,
    IOptions<ServerOptions> serverOptions,
    WebSocketCommunicatorFactory webSocketCommunicatorFactory,
    ConnectionHandler connectionHandler
) : BackgroundService
{
    private HttpListener? _wsListener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wsPort = serverOptions.Value.Hosting.WebSocketPort;

        try
        {
            _wsListener = new HttpListener();

            if (!TryStartListener(wsPort))
            {
                logger.LogWarning("WebSocket listener could not be started on port {Port}. " +
                    "WebSocket connections will be unavailable.", wsPort);
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _wsListener.GetContextAsync().WaitAsync(stoppingToken);

                    if (!context.Request.IsWebSocketRequest)
                    {
                        logger.LogWarning("Received non-WebSocket HTTP request from {RemoteEndpoint}",
                            context.Request.RemoteEndPoint);
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }

                    logger.LogInformation("WebSocket connection from {RemoteEndpoint}",
                        context.Request.RemoteEndPoint);
                    var communicator = await webSocketCommunicatorFactory.InitialiseAsync(context, stoppingToken);
                    connectionHandler.AcceptConnection(communicator);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException ex) when (stoppingToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "HttpListener interrupted during shutdown");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error accepting WebSocket connection");
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("WebSocket listener cancellation requested");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in WebSocket listener");
            throw;
        }
        finally
        {
            logger.LogInformation("WebSocket listener stopped");
        }
    }

    /// <summary>
    /// Attempts to start the HttpListener with various prefix patterns.
    /// Returns true if the listener started successfully.
    /// </summary>
    private bool TryStartListener(int port)
    {
        // HttpListener binding patterns: * works on Linux, + on Windows
        var prefixPatterns = new[]
        {
            $"http://*:{port}/",
            $"http://+:{port}/"
        };

        foreach (var prefix in prefixPatterns)
        {
            try
            {
                _wsListener!.Prefixes.Clear();
                _wsListener.Prefixes.Add(prefix);
                _wsListener.Start();
                logger.LogInformation("WebSocket listener started on {Prefix} (all interfaces)", prefix);
                return true;
            }
            catch (HttpListenerException ex)
            {
                logger.LogWarning(ex, "Failed to bind WebSocket to {Prefix}, trying next pattern...", prefix);
            }
        }

        // Last resort: localhost-only binding
        logger.LogWarning("Could not bind to any public interface, attempting localhost fallback...");

        try
        {
            _wsListener!.Prefixes.Clear();
            _wsListener.Prefixes.Add($"http://localhost:{port}/");
            _wsListener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _wsListener.Start();
            logger.LogWarning("WebSocket listener started on localhost only (port {Port}). " +
                "External connections will not work.", port);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start WebSocket listener even on localhost");
            return false;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        DisposeListener();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        DisposeListener();
        base.Dispose();
    }

    private void DisposeListener()
    {
        if (_wsListener == null)
        {
            return;
        }

        try
        {
            _wsListener.Stop();
            _wsListener.Close();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        _wsListener = null;
    }
}
