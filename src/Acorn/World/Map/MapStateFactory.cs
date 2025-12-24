using Acorn.Database.Repository;
using Acorn.World.Services;
using Microsoft.Extensions.Logging;

namespace Acorn.World.Map;

public class MapStateFactory(
    IDataFileRepository dataRepository,
    IFormulaService formulaService,
    IMapTileService tileService,
    IMapBroadcastService broadcastService,
    INpcCombatService npcCombatService,
    ILogger<MapState> logger)
{
    public MapState Create(MapWithId data)
        => new(data, dataRepository, formulaService, tileService, broadcastService, npcCombatService, logger);
}