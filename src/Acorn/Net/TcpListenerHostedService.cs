using System.Net;
using System.Net.Sockets;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

/// <summary>
/// Listens for incoming TCP connections on the configured port.
/// Each accepted connection is handed off to <see cref="ConnectionHandler"/>.
/// </summary>
public class TcpListenerHostedService(
    ILogger<TcpListenerHostedService> logger,
    IStatsReporter statsReporter,
    IOptions<ServerOptions> serverOptions,
    TcpCommunicatorFactory tcpCommunicatorFactory,
    ConnectionHandler connectionHandler
) : BackgroundService
{
    private readonly TcpListener _listener = new(IPAddress.Any, serverOptions.Value.Hosting.Port);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await statsReporter.Report();
            _listener.Start();
            logger.LogInformation("TCP listener started on {Endpoint}", _listener.LocalEndpoint);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync(stoppingToken);
                    var communicator = tcpCommunicatorFactory.Initialise(tcpClient);
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
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error accepting TCP connection");
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("TCP listener cancellation requested");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in TCP listener");
            throw;
        }
        finally
        {
            logger.LogInformation("TCP listener stopped");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener.Stop();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        _listener.Dispose();
        base.Dispose();
    }
}
