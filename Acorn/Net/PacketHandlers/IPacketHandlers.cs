using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net.PacketHandlers;

public interface IHandler
{
    Task HandleAsync(ConnectionHandler connectionHandler, object packet);
}

public interface IHandler<TOut>
{
    Task<TOut> HandleAsync(ConnectionHandler connectionHandler, object packet);
}

public interface IPacketHandler<in TPacket> : IHandler where TPacket : IPacket
{
    Task HandleAsync(ConnectionHandler connectionHandler, TPacket packet);
}

public interface IPacketHandler<in TPacket, TOut> : IHandler<TOut> where TPacket : IPacket
{
    Task<TOut> HandleAsync(ConnectionHandler connectionHandler, TPacket packet);
}