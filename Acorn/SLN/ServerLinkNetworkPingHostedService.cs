using System.Reflection;
using Acorn.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

namespace Acorn.SLN;

public class ServerLinkNetworkPingHostedService(
    ILogger<ServerLinkNetworkPingHostedService> logger,
    IOptions<ServerOptions> serverOptions,
    IServerLinkNetworkClient client)
    : IHostedService
{
    private readonly SLNOptions _slnOptions = serverOptions.Value.Hosting.SLN;
    private readonly HostingOptions _hostingOptions = serverOptions.Value.Hosting;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (CanStart() is false)
        {
            return;
        }

        logger.LogDebug("Starting ServerLinkNetworkPingHostedService");
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(_slnOptions.PingRate));
        while (cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                logger.LogDebug("Current assembly version {Version}", Assembly.GetExecutingAssembly().GetName()?.Version?.ToString());
                var response = await client.CheckSlnAsync(
                    "Acorn",
                    Assembly.GetExecutingAssembly().GetName()?.Version?.ToString() ?? throw new Exception("Could not get version of current assembly"),
                    _hostingOptions.HostName,
                    _hostingOptions.Port,
                    _slnOptions.ServerName,
                    _slnOptions.Site,
                    _slnOptions.Zone,
                    0,
                    2,
                    _slnOptions.PingRate * 60
                );

                logger.LogDebug("Response from SLN: {Response}", response);
                await timer.WaitForNextTickAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while getting sln response {Message}", e.Message);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Stopping ServerLinkNetworkPingHostedService");
        return Task.CompletedTask;
    }

    private bool CanStart()
    {
        if (_slnOptions.PingRate <= 0)
        {
            logger.LogInformation("SLN PingRate is set to {PingRate}, not starting SLN ping service", _slnOptions.PingRate);
            return false;
        }

        if (string.IsNullOrEmpty(_slnOptions.ServerName) || string.IsNullOrEmpty(_slnOptions.Site) || string.IsNullOrEmpty(_slnOptions.Zone))
        {
            logger.LogInformation("SLN ServerName, Site or Zone is not set, not starting SLN ping service");
            return false;
        }

        if (_slnOptions.Enabled is false)
        {
            logger.LogInformation("SLN is disabled, not starting SLN ping service");
            return false;
        }

        return true;
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