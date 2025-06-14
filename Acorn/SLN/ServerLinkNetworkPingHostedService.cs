using System.Reflection;
using Acorn.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

namespace Acorn.SLN;

public class ServerLinkNetworkPingHostedService : IHostedService
{
    private readonly ILogger<ServerLinkNetworkPingHostedService> _logger;
    private readonly IServerLinkNetworkClient _client;
    private readonly SLNOptions _slnOptions;
    private readonly ServerOptions _serverOptions;

    public ServerLinkNetworkPingHostedService(
        ILogger<ServerLinkNetworkPingHostedService> logger,
        IOptions<SLNOptions> slnOptions,
        IOptions<ServerOptions> serverOptions,
        IServerLinkNetworkClient client
    )
    {
        _logger = logger;
        _client = client;
        _slnOptions = slnOptions.Value;
        _serverOptions = serverOptions.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting ServerLinkNetworkPingHostedService");
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(_slnOptions.PingRate));
        while (cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                _logger.LogDebug("Current assembly version {Version}", Assembly.GetExecutingAssembly().GetName()?.Version?.ToString());
                var response = await _client.CheckSlnAsync(
                    "Acorn",
                    Assembly.GetExecutingAssembly().GetName()?.Version?.ToString() ?? throw new Exception("Could not get version of current assembly"),
                    _serverOptions.Hostname,
                    _serverOptions.Port,
                    _serverOptions.ServerName,
                    _serverOptions.Site,
                    _slnOptions.Zone,
                    0,
                    2,
                    _slnOptions.PingRate * 60
                );

                _logger.LogDebug("Response from SLN: {Response}", response);
                await timer.WaitForNextTickAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while getting sln response {Message}", e.Message);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping ServerLinkNetworkPingHostedService");
        return Task.CompletedTask;
    }
}

public interface IServerLinkNetworkClient
{
    [Get("/check")]
    public Task<string> CheckSlnAsync(
        string software,
        string v,
        string host,
        int port,
        string name,
        string url,
        string zone,
        int clientMajorVersion,
        int clientMinorVersion,
        int retry
    );
}