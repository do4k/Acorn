using Acorn.Net;
using Acorn.World.Npc;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.World.Services.Npc;

/// <summary>
///     Result of an NPC movement attempt.
/// </summary>
public record NpcMoveResult(bool Moved, Direction Direction, Coords NewCoords);
