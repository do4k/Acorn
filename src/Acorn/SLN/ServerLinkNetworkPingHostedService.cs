using System.Diagnostics;
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
    : BackgroundService
{
    private readonly HostingOptions _hostingOptions = serverOptions.Value.Hosting;
    private readonly SLNOptions _slnOptions = serverOptions.Value.Hosting.SLN;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (CanStart() is false)
        {
            return;
        }

        logger.LogDebug("Starting ServerLinkNetworkPingHostedService");

        // Initial check
        await CheckSlnAsync();

        var timer = new PeriodicTimer(TimeSpan.FromMinutes(_slnOptions.PingRate));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await CheckSlnAsync();
        }
    }

    private async Task CheckSlnAsync()
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            logger.LogDebug("Current assembly version {Version}",
                Assembly.GetExecutingAssembly().GetName()?.Version?.ToString());
            var response = await client.CheckSlnAsync(
                "Acorn",
                Assembly.GetExecutingAssembly().GetName()?.Version?.ToString() ??
                throw new Exception("Could not get version of current assembly"),
                _hostingOptions.HostName,
                _hostingOptions.Port,
                _slnOptions.ServerName,
                _slnOptions.Site,
                _slnOptions.Zone,
                0,
                2,
                _slnOptions.PingRate * 60
            );

            logger.LogInformation("SLN check completed in {ElapsedMilliseconds} ms",
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            logger.LogDebug("Response from SLN: {Response}", response);
        }
        catch (Exception e)
        {
            // Timeouts (TaskCanceledException) and other transport errors land here;
            // recording the elapsed time makes it obvious the HttpClient timeout fired.
            logger.LogError(e, "Error while getting sln response after {ElapsedMilliseconds} ms: {Message}",
                Stopwatch.GetElapsedTime(start).TotalMilliseconds, e.Message);
        }
    }

    private bool CanStart()
    {
        if (_slnOptions.PingRate <= 0)
        {
            logger.LogInformation("SLN PingRate is set to {PingRate}, not starting SLN ping service",
                _slnOptions.PingRate);
            return false;
        }

        if (string.IsNullOrEmpty(_slnOptions.ServerName) || string.IsNullOrEmpty(_slnOptions.Site) ||
            string.IsNullOrEmpty(_slnOptions.Zone))
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
