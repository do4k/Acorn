using Acorn.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Acorn.SLN;

/// <summary>
///     Configures the <see cref="HttpClient" /> that backs the <see cref="IServerLinkNetworkClient" />
///     Refit registration in the composition root.
/// </summary>
public static class ServerLinkNetworkHttpClientConfiguration
{
    /// <summary>
    ///     Applies the base address, user agent and per-request timeout from
    ///     <see cref="SLNOptions" /> so a hung SLN endpoint cannot tie up the
    ///     background check for the <see cref="HttpClient" /> default of 100 seconds.
    /// </summary>
    /// <param name="services">Provider used to resolve the server options.</param>
    /// <param name="client">The <see cref="HttpClient" /> created for the Refit client.</param>
    public static void Configure(IServiceProvider services, HttpClient client)
    {
        var slnOptions = services.GetRequiredService<IOptions<ServerOptions>>().Value.Hosting.SLN;
        client.BaseAddress = new Uri(slnOptions.Url);
        client.DefaultRequestHeaders.Add("User-Agent", slnOptions.UserAgent);
        client.Timeout = TimeSpan.FromSeconds(slnOptions.TimeoutSeconds);
    }
}
