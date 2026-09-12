using System.Reflection;
using Acorn.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

namespace Acorn.SLN;

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