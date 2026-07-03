using Acorn.Data;
using Acorn.Extensions;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Book;

[RequiresCharacter]
public class BookRequestClientPacketHandler(
    IWorldQueries world,
    IQuestDataRepository questDataRepository,
    ILogger<BookRequestClientPacketHandler> logger)
    : IPacketHandler<BookRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, BookRequestClientPacket packet)
    {
        logger.LogInformation("Player {Character} requesting book from player {PlayerId}",
            player.Character!.Name, packet.PlayerId);

        var targetPlayer = world.GetPlayer(packet.PlayerId);
        if (targetPlayer?.Character is null)
        {
            return;
        }

        var character = targetPlayer.Character;

        var questNames = character.Quests
            .Where(q => q.DoneAt == null)
            .Select(q => questDataRepository.GetQuest(q.QuestId)?.Name)
            .Where(name => name != null)
            .Cast<string>()
            .ToList();

        await player.Send(new BookReplyServerPacket
        {
            Details = new CharacterDetails
            {
                Name = character.Name,
                Home = character.Home ?? "",
                Admin = character.Admin,
                Partner = character.Partner ?? "",
                Title = character.Title ?? "",
                Guild = "", // TODO: Implement guilds
                GuildRank = "", // TODO: Implement guilds
                PlayerId = packet.PlayerId,
                ClassId = character.Class,
                Gender = character.Gender
            },
            Icon = (int)character.Admin switch
            {
                0 => CharacterIcon.Player,
                1 or 2 or 3 => CharacterIcon.Gm,
                _ => CharacterIcon.Hgm
            },
            QuestNames = questNames
        });
    }

}
