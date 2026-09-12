using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net.PacketHandlers;

/// <summary>
/// Typed packet handler interface. Handlers only need to implement this interface.
/// The dispatch mechanism uses cached reflection to invoke HandleAsync directly.
/// </summary>
public interface IPacketHandler<in TPacket> : IPacketHandler where TPacket : IPacket
{
    Task HandleAsync(PlayerState playerState, TPacket packet);
}