using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Gemini;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Options;
using Acorn.World.Map;
using Acorn.World.Services;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using NSubstitute;

namespace Acorn.Tests.TestSupport;

/// <summary>
///     In-memory communicator that records every payload the server sends, so unit
///     tests can assert whether a handler delivered a packet without a live socket.
/// </summary>
internal sealed class CapturingCommunicator : ICommunicator
{
    public List<byte[]> Sent { get; } = [];

    public bool IsConnected => false;

    public Task Send(IEnumerable<byte> bytes)
    {
        Sent.Add(bytes.ToArray());
        return Task.CompletedTask;
    }

    public Stream Receive() => Stream.Null;

    public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public string GetConnectionOrigin() => "test";
}
