using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net.PacketHandlers;

public interface IHandler
{
    Task HandleAsync(PlayerConnection playerConnection, object packet);
}

public interface IHandler<TOut>
{
    Task<TOut> HandleAsync(PlayerConnection playerConnection, object packet);
}

public interface IPacketHandler<in TPacket> : IHandler where TPacket : IPacket
{
    Task HandleAsync(PlayerConnection playerConnection, TPacket packet);
}

public interface IPacketHandler<in TPacket, TOut> : IHandler<TOut> where TPacket : IPacket
{
    Task<TOut> HandleAsync(PlayerConnection playerConnection, TPacket packet);
}