using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollRequestClientPacketHandler : IPacketHandler<PaperdollRequestClientPacket>
{
    private readonly IWorldQueries _world;

    public PaperdollRequestClientPacketHandler(IWorldQueries world, IPaperdollService paperdollService,
        ILogger<PaperdollRequestClientPacketHandler> logger)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerState playerState, PaperdollRequestClientPacket packet)
    {
        // Get the player whose paperdoll was requested by session ID
        var targetPlayer = _world.GetPlayer(packet.PlayerId);

        if (targetPlayer?.Character is null)
        {
            return; // Player not found or has no character
        }

        var character = targetPlayer.Character;

        // Send the paperdoll reply with target player's details and equipment
        await playerState.Send(new PaperdollReplyServerPacket
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
            Equipment = character.Equipment()
        });
    }

}