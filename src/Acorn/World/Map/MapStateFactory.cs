using Acorn.Database.Repository;
using Acorn.Options;
using Acorn.World.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.World.Map;

public class MapStateFactory(
    IDataFileRepository dataRepository,
    IFormulaService formulaService,
    IMapTileService tileService,
    IMapBroadcastService broadcastService,
    INpcCombatService npcCombatService,
    IPlayerController playerController,
    INpcController npcController,
    IOptions<ServerOptions> serverOptions,
    ILogger<MapState> logger)
{
    public MapState Create(MapWithId data)
        => new(data, dataRepository, formulaService, tileService, broadcastService, npcCombatService, playerController, npcController, serverOptions.Value.PlayerRecoverRate, logger);
}