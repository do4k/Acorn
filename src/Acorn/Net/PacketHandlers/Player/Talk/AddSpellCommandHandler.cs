using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Net.Services;
using Microsoft.Extensions.Logging;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class AddSpellCommandHandler : ITalkHandler
{
    private readonly IDbRepository<Database.Models.Character> _characterRepository;
    private readonly ILogger<AddSpellCommandHandler> _logger;
    private readonly INotificationService _notifications;

    public AddSpellCommandHandler(
        IDbRepository<Database.Models.Character> characterRepository,
        ILogger<AddSpellCommandHandler> logger,
        INotificationService notifications)
    {
        _characterRepository = characterRepository;
        _logger = logger;
        _notifications = notifications;
    }

    public bool CanHandle(string command)
    {
        return command.Equals("addspell", StringComparison.InvariantCultureIgnoreCase)
               || command.Equals("spell", StringComparison.InvariantCultureIgnoreCase);
    }

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 2)
        {
            await _notifications.ServerAnnouncement(playerState,
                "Usage: $[addspell | spell] <character_name> <spell_id> [level]");
            return;
        }

        var characterName = args[0];
        
        if (!int.TryParse(args[1], out var spellId))
        {
            await _notifications.ServerAnnouncement(playerState, $"Invalid spell ID: {args[1]}");
            return;
        }

        // Parse level if provided, default to 0
        var level = 0;
        if (args.Length >= 3 && !int.TryParse(args[2], out level))
        {
            await _notifications.ServerAnnouncement(playerState, $"Invalid level: {args[2]}");
            return;
        }

        // Load the character
        var character = await _characterRepository.GetByKeyAsync(characterName);
        if (character is null)
        {
            await _notifications.ServerAnnouncement(playerState, $"Character '{characterName}' not found");
            return;
        }

        // Check if the character already has this spell
        var existingSpell = character.Spells.FirstOrDefault(s => s.SpellId == spellId);
        if (existingSpell is not null)
        {
            await _notifications.ServerAnnouncement(playerState, 
                $"Character '{characterName}' already has spell {spellId} at level {existingSpell.Level}");
            return;
        }

        // Add the spell
        var characterSpell = new CharacterSpell
        {
            CharacterName = character.Name!,
            SpellId = spellId,
            Level = level
        };

        character.Spells.Add(characterSpell);
        await _characterRepository.UpdateAsync(character);

        _logger.LogInformation(
            "Admin '{AdminName}' added spell {SpellId} (level {Level}) to character '{CharacterName}'",
            playerState.Character?.Name, spellId, level, characterName);

        await _notifications.ServerAnnouncement(playerState,
            $"Added spell {spellId} (level {level}) to '{characterName}'");
    }
}
