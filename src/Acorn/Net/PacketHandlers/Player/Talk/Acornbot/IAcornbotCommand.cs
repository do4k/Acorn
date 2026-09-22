namespace Acorn.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     Interface for the curated set of commands players can run by whispering
///     Acornbot. Implementations are discovered and registered via
///     <c>AddAllOfType&lt;IAcornbotCommand&gt;()</c>.
/// </summary>
public interface IAcornbotCommand : ICommandHandler
{
    /// <summary>
    ///     One-line description shown in the bot's help reply.
    /// </summary>
    string Description { get; }
}
