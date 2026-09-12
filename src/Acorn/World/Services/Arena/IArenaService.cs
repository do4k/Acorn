using Acorn.Net;
using Acorn.Options;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Arena;

public interface IArenaService
{
    bool IsArenaEnabled { get; }
    int ArenaMapId { get; }

    Task JoinArenaAsync(PlayerState player);
    Task LeaveArenaAsync(PlayerState player);
    Task HandleArenaAttackAsync(PlayerState attacker, PlayerState target);
    Task ProcessArenaDeathAsync(PlayerState deadPlayer);
}
