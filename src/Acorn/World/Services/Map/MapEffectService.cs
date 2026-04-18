using Acorn.Extensions;
using Acorn.Net;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Map;

public class MapEffectService(
    IMapTileService tileService,
    ILogger<MapEffectService> logger) : IMapEffectService
{
    public async Task EffectOnCoordsAsync(MapState map, IReadOnlyList<Coords> coords, int effectId)
    {
        if (coords.Count == 0)
        {
            return;
        }

        var packet = new EffectAgreeServerPacket
        {
            Effects = coords.Select(c => new TileEffect
            {
                Coords = c,
                EffectId = effectId
            }).ToList()
        };

        // Only send to players who are in client range of any of the effect coordinates
        var tasks = map.Players.Values
            .Where(p => p.Character is not null)
            .Where(p => coords.Any(c => tileService.InClientRange(p.Character!.AsCoords(), c)))
            .Select(p => p.Send(packet));

        await Task.WhenAll(tasks);

        logger.LogDebug("Played effect {EffectId} at {Count} coordinates on map {MapId}",
            effectId, coords.Count, map.Id);
    }

    public async Task EffectOnPlayersAsync(MapState map, IReadOnlyList<int> playerIds, int effectId)
    {
        if (playerIds.Count == 0)
        {
            return;
        }

        var packet = new EffectPlayerServerPacket
        {
            Effects = playerIds.Select(id => new PlayerEffect
            {
                PlayerId = id,
                EffectId = effectId
            }).ToList()
        };

        // Only send to players who are in client range of any of the target players
        var tasks = map.Players.Values
            .Where(p => p.Character is not null)
            .Where(observer => playerIds.Any(targetId =>
            {
                var target = map.Players.Values.FirstOrDefault(t => t.SessionId == targetId);
                if (target?.Character is null || target.Character.Hidden)
                {
                    return false;
                }

                return tileService.InClientRange(observer.Character!.AsCoords(), target.Character.AsCoords());
            }))
            .Select(p => p.Send(packet));

        await Task.WhenAll(tasks);

        logger.LogDebug("Played effect {EffectId} on {Count} players on map {MapId}",
            effectId, playerIds.Count, map.Id);
    }

    public async Task QuakeAsync(MapState map, int magnitude)
    {
        magnitude = Math.Clamp(magnitude, 1, 8);

        await map.BroadcastPacket(new EffectUseServerPacket
        {
            Effect = MapEffect.Quake,
            EffectData = new EffectUseServerPacket.EffectDataQuake
            {
                QuakeStrength = magnitude
            }
        });

        logger.LogDebug("Triggered quake magnitude {Magnitude} on map {MapId}", magnitude, map.Id);
    }
}
