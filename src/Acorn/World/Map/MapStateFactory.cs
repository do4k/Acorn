using Acorn.Database.Repository;
using Acorn.Options;
using Acorn.World.Services;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.World.Map;

public class MapStateFactory(
    IDataFileRepository dataRepository,
    IMapBroadcastService broadcastService,
    IMapController mapController,
    INpcController npcController,
    IOptions<ServerOptions> serverOptions,
    ILogger<MapState> logger)
{
    public MapState Create(MapWithId data)
        => new(data, dataRepository, broadcastService, mapController, npcController, serverOptions.Value.PlayerRecoverRate, logger);
}