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
    private readonly IPaperdollService _paperdollService;
    private readonly ILogger<PaperdollRequestClientPacketHandler> _logger;

    public PaperdollRequestClientPacketHandler(IWorldQueries world, IPaperdollService paperdollService, ILogger<PaperdollRequestClientPacketHandler> logger)
    {
        _world = world;
        _paperdollService = paperdollService;
        _logger = logger;
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

        var equipment = _paperdollService.ToEquipmentPaperdoll(character.Equipment());
        
        _logger.LogInformation("Paperdoll request for {CharacterName} - Equipment: Hat={Hat}, Armor={Armor}, Weapon={Weapon}, Shield={Shield}, Boots={Boots}, Accessory={Accessory}, Necklace={Necklace}, Belt={Belt}, Gloves={Gloves}, Ring=[{Ring1},{Ring2}], Bracer=[{Bracer1},{Bracer2}], Armlet=[{Armlet1},{Armlet2}]",
            character.Name, equipment.Hat, equipment.Armor, equipment.Weapon, equipment.Shield, equipment.Boots,
            equipment.Accessory, equipment.Necklace, equipment.Belt, equipment.Gloves,
            equipment.Ring[0], equipment.Ring[1], equipment.Bracer[0], equipment.Bracer[1], equipment.Armlet[0], equipment.Armlet[1]);

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
                Gender = character.Gender,
            },
            Equipment = equipment
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
        => HandleAsync(playerState, (PaperdollRequestClientPacket)packet);
}