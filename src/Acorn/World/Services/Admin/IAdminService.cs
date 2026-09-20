using Acorn.Net;

namespace Acorn.World.Services.Admin;

/// <summary>
///     Service for admin operations such as kicking, banning, jailing, freezing, muting players.
/// </summary>
public interface IAdminService
{
    /// <summary>Kick a player from the server.</summary>
    /// <param name="silent">When true, do not announce the kick to the server.</param>
    Task KickPlayerAsync(PlayerState admin, string targetName, bool silent = false);

    /// <summary>Ban a player's account and disconnect them.</summary>
    /// <param name="silent">When true, do not announce the ban to the server.</param>
    Task BanPlayerAsync(PlayerState admin, string targetName, bool silent = false);

    /// <summary>Warp a player to the jail map.</summary>
    /// <param name="silent">When true, do not announce the jailing to the server.</param>
    Task JailPlayerAsync(PlayerState admin, string targetName, bool silent = false);

    /// <summary>Free a player from jail, warping them to their home map.</summary>
    Task FreePlayerAsync(PlayerState admin, string targetName);

    /// <summary>Freeze a player, preventing movement.</summary>
    Task FreezePlayerAsync(PlayerState admin, string targetName);

    /// <summary>Unfreeze a player, restoring movement.</summary>
    Task UnfreezePlayerAsync(PlayerState admin, string targetName);

    /// <summary>Mute a player, preventing chat.</summary>
    /// <param name="silent">When true, do not announce the mute to the server.</param>
    Task MutePlayerAsync(PlayerState admin, string targetName, bool silent = false);

    /// <summary>Unmute a player, restoring chat.</summary>
    Task UnmutePlayerAsync(PlayerState admin, string targetName);

    /// <summary>
    ///     Relay a player's help request to online admins and confirm receipt to the sender.
    /// </summary>
    Task SendHelpRequestAsync(PlayerState sender, string message);

    /// <summary>
    ///     Relay a player report to online admins, persist it to the admin board, and
    ///     confirm receipt to the sender.
    /// </summary>
    Task SendReportAsync(PlayerState sender, string reportee, string message);

    /// <summary>Send player info (stats, location) to the requesting admin.</summary>
    Task GetPlayerInfoAsync(PlayerState admin, string targetName);

    /// <summary>Send player inventory to the requesting admin.</summary>
    Task GetPlayerInventoryAsync(PlayerState admin, string targetName);

    /// <summary>Trigger a quake effect on all maps.</summary>
    Task TriggerQuakeAsync(PlayerState admin, int strength);

    /// <summary>Evacuate all players from the admin's current map.</summary>
    Task EvacuateMapAsync(PlayerState admin);

    /// <summary>Warp the requesting admin to another player's current location.</summary>
    Task WarpToPlayerAsync(PlayerState admin, string targetName);

    /// <summary>Warp another player to the requesting admin's current location.</summary>
    Task SummonPlayerAsync(PlayerState admin, string targetName);

    /// <summary>Toggle admin hidden/visible state.</summary>
    Task ToggleHideAsync(PlayerState admin);

    /// <summary>Send a server-wide global announcement.</summary>
    Task GlobalMessageAsync(PlayerState admin, string message);
}
