using System.Threading.Channels;
using Acorn.Infrastructure.Gemini;
using Acorn.Net;
using Acorn.Options;
using Acorn.World.Npc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services;

/// <summary>
///     Request to get a response from the Wise Man.
/// </summary>
public record WiseManRequest(PlayerState Player, string Query, NpcState WiseManNpc);
