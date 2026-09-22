namespace Acorn.Plugins;

/// <summary>
///     Context for an <see cref="IPluginCommand" /> invocation.
/// </summary>
public interface ICommandContext
{
    /// <summary>The player who issued the command.</summary>
    IPlayerView Player { get; }

    /// <summary>The matched command name, without prefix.</summary>
    string Command { get; }

    /// <summary>Whitespace-split arguments following the command name.</summary>
    IReadOnlyList<string> Args { get; }

    /// <summary>Replies privately to the issuing player (system message).</summary>
    Task ReplyAsync(string message);

    /// <summary>Replies to the issuing player with a server announcement.</summary>
    Task AnnounceAsync(string message);
}
