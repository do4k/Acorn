using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net.PacketHandlers;

public interface IHandler
{
    Task HandleAsync(PlayerState playerState, object packet);
}

public interface IHandler<TOut>
{
    Task<TOut> HandleAsync(PlayerState playerState, object packet);
}

public interface IPacketHandler<in TPacket> : IHandler where TPacket : IPacket
{
    Task HandleAsync(PlayerState playerState, TPacket packet);
}

public interface IPacketHandler<in TPacket, TOut> : IHandler<TOut> where TPacket : IPacket
{
    Task<TOut> HandleAsync(PlayerState playerState, TPacket packet);
}