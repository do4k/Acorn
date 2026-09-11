using Acorn.Net.Models;

namespace Acorn.Net.PacketHandlers;

/// <summary>
///     Declares the minimum <see cref="ClientState" /> a connection must have reached
///     before the decorated handler may run. Packets received earlier in the
///     handshake (Init -> Connection/Accept -> Account/Login -> Character/Welcome)
///     are rejected by the dispatch pipeline instead of reaching the handler.
/// </summary>
/// <remarks>
///     States are ordered, so a connection in a later state (e.g. <see cref="ClientState.InGame" />)
///     still satisfies a requirement for an earlier one (e.g. <see cref="ClientState.LoggedIn" />).
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresStateAttribute(ClientState state) : Attribute
{
    public ClientState State { get; } = state;
}
