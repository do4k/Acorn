namespace Acorn.Plugins;

/// <summary>
///     Adds player chat commands (typed as <c>#command args</c>) to the server.
///     Implemented by the plugin's <see cref="IAcornPlugin" /> class; the host
///     bridges it into the built-in command dispatcher.
/// </summary>
public interface IPluginCommand
{
    /// <summary>
    ///     Command names this plugin responds to, without the <c>#</c> prefix.
    ///     The first entry is treated as the primary name.
    /// </summary>
    IReadOnlyList<string> Commands { get; }

    /// <summary>
    ///     Argument usage shown by the in-game help command, e.g. <c>&lt;player&gt;</c>.
    ///     Empty when the command takes no arguments.
    /// </summary>
    string Usage => string.Empty;

    /// <summary>Handles the command invocation.</summary>
    Task HandleAsync(ICommandContext context);
}
