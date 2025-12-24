using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Warp;
using Acorn.Options;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services;

public class PlayerController : IPlayerController
{
    private readonly ILogger<PlayerController> _logger;
    private readonly IMapBroadcastService _broadcastService;
    private readonly Lazy<IWorldQueries> _worldQueries;
    private readonly ServerOptions _serverOptions;

    public PlayerController(
        ILogger<PlayerController> logger,
        IMapBroadcastService broadcastService,
        Lazy<IWorldQueries> worldQueries,
        IOptions<ServerOptions> serverOptions)
    {
        _logger = logger;
        _broadcastService = broadcastService;
        _worldQueries = worldQueries;
        _serverOptions = serverOptions.Value;
    }

    public async Task WarpAsync(PlayerState player, MapState targetMap, int x, int y, WarpEffect warpEffect = WarpEffect.None)
    {
        if (player.Character == null)
        {
            _logger.LogWarning("Cannot warp player {SessionId} - no character selected", player.SessionId);
            return;
        }

        var warpSession = new WarpSession(x, y, player, targetMap, warpEffect);
        player.WarpSession = warpSession;

        if (!warpSession.IsLocal)
        {
            if (player.CurrentMap != null)
            {
                await player.CurrentMap.NotifyLeave(player, warpEffect);
            }

            await targetMap.NotifyEnter(player, warpEffect);
        }

        await warpSession.Execute();

        _logger.LogDebug("Player {CharacterName} warped to map {MapId} at ({X}, {Y})",
            player.Character.Name, targetMap.Id, x, y);
    }

    public Task RefreshAsync(PlayerState player)
    {
        if (player.Character == null)
        {
            throw new InvalidOperationException("Cannot refresh player - no character selected");
        }

        if (player.CurrentMap == null)
        {
            throw new InvalidOperationException("Cannot refresh player - no map assigned");
        }

        return WarpAsync(player, player.CurrentMap, player.Character.X, player.Character.Y);
    }

    public async Task MoveAsync(PlayerState player, int x, int y)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            return;
        }

        player.Character.X = x;
        player.Character.Y = y;

        // Broadcast movement to other players
        await _broadcastService.BroadcastPacket(
            player.CurrentMap.Players,
            new WalkPlayerServerPacket
            {
                PlayerId = player.SessionId,
                Direction = player.Character.Direction,
                Coords = new Coords { X = x, Y = y }
            },
            player);
    }

    public async Task FaceAsync(PlayerState player, Direction direction)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            return;
        }

        player.Character.Direction = direction;

        // Broadcast face direction to other players
        await _broadcastService.BroadcastPacket(
            player.CurrentMap.Players,
            new FacePlayerServerPacket
            {
                PlayerId = player.SessionId,
                Direction = direction
            },
            player);
    }

    public async Task SitAsync(PlayerState player)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            return;
        }

        player.Character.SitState = SitState.Floor;

        await _broadcastService.BroadcastPacket(
            player.CurrentMap.Players,
            new SitPlayerServerPacket
            {
                PlayerId = player.SessionId,
                Coords = new Coords { X = player.Character.X, Y = player.Character.Y },
                Direction = player.Character.Direction
            });
    }

    public async Task StandAsync(PlayerState player)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            return;
        }

        player.Character.SitState = SitState.Stand;

        await _broadcastService.BroadcastPacket(
            player.CurrentMap.Players,
            new SitPlayerServerPacket
            {
                PlayerId = player.SessionId,
                Coords = new Coords { X = player.Character.X, Y = player.Character.Y },
                Direction = player.Character.Direction
            });
    }

    public async Task DieAsync(PlayerState player)
    {
        if (player.Character == null)
        {
            _logger.LogWarning("Cannot die player {SessionId} - no character selected", player.SessionId);
            return;
        }

        _logger.LogInformation("Player {CharacterName} died", player.Character.Name);

        // Get rescue spawn location (fall back to new character spawn if not configured)
        var rescue = _serverOptions.Rescue ?? new RescueOptions
        {
            Map = _serverOptions.NewCharacter.Map,
            X = _serverOptions.NewCharacter.X,
            Y = _serverOptions.NewCharacter.Y
        };

        var rescueMap = _worldQueries.Value.FindMap(rescue.Map);
        if (rescueMap == null)
        {
            _logger.LogError("Could not find rescue map {MapId}", rescue.Map);
            return;
        }

        // Reset HP to max (no item drops as per user request)
        player.Character.Hp = player.Character.MaxHp;

        // Warp to rescue location
        await WarpAsync(player, rescueMap, rescue.X, rescue.Y, WarpEffect.None);

        _logger.LogDebug("Player {CharacterName} respawned at map {MapId} ({X}, {Y})",
            player.Character.Name, rescue.Map, rescue.X, rescue.Y);
    }
}
