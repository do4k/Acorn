namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     Shared contract for in-game chat commands. Implemented by both admin
///     (<c>$</c>) and player (<c>#</c>) command handlers.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    ///     The command name(s) this handler responds to, without the <c>$</c> or
    ///     <c>#</c> prefix. The first entry is treated as the primary name.
    /// </summary>
    IReadOnlyList<string> Commands { get; }

    /// <summary>
    ///     Argument usage shown by the help command, e.g. <c>&lt;player&gt;</c>.
    ///     Empty when the command takes no arguments.
    /// </summary>
    string Usage => string.Empty;

    /// <summary>
    ///     Handles the command for <paramref name="playerState" />.
    /// </summary>
    Task HandleAsync(PlayerState playerState, string command, params string[] args);
}
