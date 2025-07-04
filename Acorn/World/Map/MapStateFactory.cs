using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;

namespace Acorn.World.Map;

public class MapStateFactory(IDataFileRepository dataRepository, ILogger<MapState> logger)
{
    public MapState Create(MapWithId data, WorldState worldState)
        => new(data, worldState, dataRepository, logger);
}