using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net.PacketHandlers;

/// <summary>
/// Marker interface for packet handlers. Used for IoC resolution.
/// </summary>
public interface IPacketHandler { }

/// <summary>
/// Typed packet handler interface. Handlers only need to implement this interface.
/// The dispatch mechanism uses cached reflection to invoke HandleAsync directly.
/// </summary>
public interface IPacketHandler<in TPacket> : IPacketHandler where TPacket : IPacket
{
    Task HandleAsync(PlayerState playerState, TPacket packet);
}