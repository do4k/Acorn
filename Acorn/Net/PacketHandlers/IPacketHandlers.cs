using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net.PacketHandlers;

public interface IPacketHandler
{
    Task HandleAsync(PlayerState playerState, IPacket packet);
}

public interface IPacketHandler<in TPacket> : IPacketHandler where TPacket : IPacket
{
    Task HandleAsync(PlayerState playerState, TPacket packet);
}