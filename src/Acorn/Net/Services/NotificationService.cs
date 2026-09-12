using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.Services;

public class NotificationService : INotificationService
{
    public Task ServerAnnouncement(PlayerState player, string message)
    {
        return player.Send(new TalkServerServerPacket { Message = message });
    }

    public Task SystemMessage(PlayerState player, string message)
    {
        return player.Send(new TalkMsgServerPacket { Message = message, PlayerName = "System" });
    }

    public Task AdminMessage(PlayerState player, string message)
    {
        return player.Send(new TalkAdminServerPacket
        { Message = message, PlayerName = player.Character?.Name ?? "System" });
    }
}