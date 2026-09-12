using Acorn.Tests.Support;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     Server fixture with walk timestamp enforcement enabled (eoserv's
///     EnforceTimestamps). The default test fixture disables it so the shared
///     client helper can send a fixed timestamp of 0.
/// </summary>
public class TimestampEnforcingServerFixture : TestServerFixture
{
    protected override bool EnforceTimestamps => true;
}
