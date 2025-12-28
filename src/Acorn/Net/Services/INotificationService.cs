using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.Services;

/// <summary>
/// Service for sending notifications and messages to players.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a server announcement message (shown globally with server prefix).
    /// Maps to TalkServerServerPacket.
    /// </summary>
    Task ServerAnnouncement(PlayerState player, string message);

    /// <summary>
    /// Sends a system message to a player (shown only to them, no prefix).
    /// Maps to TalkMsgServerPacket.
    /// </summary>
    Task SystemMessage(PlayerState player, string message);

    /// <summary>
    /// Sends an admin message to a player.
    /// Maps to TalkAdminServerPacket.
    /// </summary>
    Task AdminMessage(PlayerState player, string message);
}

public class NotificationService : INotificationService
{
    public Task ServerAnnouncement(PlayerState player, string message)
        => player.Send(new TalkServerServerPacket { Message = message });

    public Task SystemMessage(PlayerState player, string message)
        => player.Send(new TalkMsgServerPacket { Message = message, PlayerName = "System" });

    public Task AdminMessage(PlayerState player, string message)
        => player.Send(new TalkAdminServerPacket { Message = message, PlayerName = player.Character?.Name ?? "System" });
}
