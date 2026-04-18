using Acorn.Database.Repository;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Jukebox;

[RequiresCharacter]
public class JukeboxUseClientPacketHandler(
    ILogger<JukeboxUseClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IOptions<JukeboxOptions> jukeboxOptions)
    : IPacketHandler<JukeboxUseClientPacket>
{
    public async Task HandleAsync(PlayerState player, JukeboxUseClientPacket packet)
    {
        if (player.CurrentMap is null || player.Character is null)
        {
            return;
        }

        var options = jukeboxOptions.Value;
        var instrumentId = packet.InstrumentId;
        var noteId = packet.NoteId;

        if (instrumentId <= 0 || noteId <= 0 || noteId > options.MaxNoteId)
        {
            return;
        }

        // Must have a weapon equipped
        if (player.Character.Paperdoll.Weapon == 0)
        {
            return;
        }

        // The instrument ID must be in the allowed list
        if (!options.InstrumentItems.Contains(instrumentId))
        {
            return;
        }

        // Verify the equipped weapon's spec1 matches the instrument ID
        var weaponData = dataFileRepository.Eif.GetItem(player.Character.Paperdoll.Weapon);
        if (weaponData is null || weaponData.Spec1 != instrumentId)
        {
            return;
        }

        // Verify the player has a bard-type spell
        var hasBardSpell = player.Character.Spells.Items.Any(s =>
        {
            var spellData = dataFileRepository.Esf.GetSkill(s.Id);
            return spellData?.Type == SkillType.Bard;
        });

        if (!hasBardSpell)
        {
            return;
        }

        // Broadcast instrument sound to nearby players (excluding the player themselves,
        // as the client handles local playback)
        await player.CurrentMap.BroadcastPacket(new JukeboxMsgServerPacket
        {
            PlayerId = player.SessionId,
            Direction = player.Character.Direction,
            InstrumentId = instrumentId,
            NoteId = noteId
        }, player);

        logger.LogDebug("Player {Character} played instrument {InstrumentId} note {NoteId}",
            player.Character.Name, instrumentId, noteId);
    }
}
