using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     Interface for admin <c>$</c> commands.
/// </summary>
public interface ITalkHandler : ICommandHandler
{
    /// <summary>
    ///     Minimum admin level required to run this command. Enforced by the
    ///     command dispatcher before <see cref="ICommandHandler.HandleAsync" />.
    /// </summary>
    AdminLevel RequiredLevel => AdminLevel.Spy;
}
