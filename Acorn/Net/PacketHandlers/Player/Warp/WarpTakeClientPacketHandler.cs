﻿using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

internal class WarpTakeClientPacketHandler : IPacketHandler<WarpTakeClientPacket>
{
    private readonly ILogger<WarpTakeClientPacketHandler> _logger;
    private readonly WorldState _world;

    public WarpTakeClientPacketHandler(WorldState world, ILogger<WarpTakeClientPacketHandler> logger)
    {
        _world = world;
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState, WarpTakeClientPacket packet)
    {
        if (packet.SessionId != playerState.SessionId)
        {
            _logger.LogError("Sesison ID was not as expected for player {Player}", playerState.Account?.Username);
            return;
        }

        var foundMap = _world.Maps.TryGetValue(packet.MapId, out var map);
        if (foundMap is false || map is null)
        {
            _logger.LogError("Map with ID {MapId} not found for player {Player}", packet.MapId, playerState.Account?.Username);
            return;
        }

        var writer = new EoWriter();
        map.Data.Serialize(writer);

        await playerState.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.WarpMap,
            ReplyCodeData = new InitInitServerPacket.ReplyCodeDataWarpMap
            {
                MapFile = new MapFile
                {
                    Content = writer.ToByteArray()
                }
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, object packet)
    {
        return HandleAsync(playerState, (WarpTakeClientPacket)packet);
    }
}