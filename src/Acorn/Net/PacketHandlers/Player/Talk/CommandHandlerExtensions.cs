namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     Helpers for matching a command name against a handler's declared
///     <see cref="ICommandHandler.Commands" />.
/// </summary>
internal static class CommandHandlerExtensions
{
    public static bool CanHandle(this ICommandHandler handler, string command)
        => handler.Commands.Contains(command, StringComparer.InvariantCultureIgnoreCase);
}
