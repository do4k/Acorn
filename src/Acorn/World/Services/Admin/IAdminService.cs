using Acorn.Net;

namespace Acorn.World.Services.Admin;

/// <summary>
///     Service for admin operations such as kicking, banning, jailing, freezing, muting players.
/// </summary>
public interface IAdminService
{
    /// <summary>Kick a player from the server.</summary>
    Task KickPlayerAsync(PlayerState admin, string targetName);

    /// <summary>Ban a player's account and disconnect them.</summary>
    Task BanPlayerAsync(PlayerState admin, string targetName);

    /// <summary>Warp a player to the jail map.</summary>
    Task JailPlayerAsync(PlayerState admin, string targetName);

    /// <summary>Free a player from jail, warping them to their home map.</summary>
    Task FreePlayerAsync(PlayerState admin, string targetName);

    /// <summary>Freeze a player, preventing movement.</summary>
    Task FreezePlayerAsync(PlayerState admin, string targetName);

    /// <summary>Unfreeze a player, restoring movement.</summary>
    Task UnfreezePlayerAsync(PlayerState admin, string targetName);

    /// <summary>Mute a player, preventing chat.</summary>
    Task MutePlayerAsync(PlayerState admin, string targetName);

    /// <summary>Unmute a player, restoring chat.</summary>
    Task UnmutePlayerAsync(PlayerState admin, string targetName);

    /// <summary>Send player info (stats, location) to the requesting admin.</summary>
    Task GetPlayerInfoAsync(PlayerState admin, string targetName);

    /// <summary>Send player inventory to the requesting admin.</summary>
    Task GetPlayerInventoryAsync(PlayerState admin, string targetName);

    /// <summary>Trigger a quake effect on all maps.</summary>
    Task TriggerQuakeAsync(PlayerState admin, int strength);

    /// <summary>Evacuate all players from the admin's current map.</summary>
    Task EvacuateMapAsync(PlayerState admin);

    /// <summary>Toggle admin hidden/visible state.</summary>
    Task ToggleHideAsync(PlayerState admin);

    /// <summary>Send a server-wide global announcement.</summary>
    Task GlobalMessageAsync(PlayerState admin, string message);
}
