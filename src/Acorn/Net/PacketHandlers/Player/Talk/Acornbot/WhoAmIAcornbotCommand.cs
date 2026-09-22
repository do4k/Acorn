using System.Globalization;
using Acorn.Database.Repository;
using Acorn.Game.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     Acornbot's read-only <c>whoami</c> command: a private summary of the
///     whispering player's own character - identity, location, vitals and
///     resources - without opening any windows.
/// </summary>
public class WhoAmIAcornbotCommand(
    IAcornbotReplyChannel replies,
    IInventoryService inventoryService,
    IDataFileRepository dataFiles) : IAcornbotCommand
{
    private const int GoldItemId = 1;

    public IReadOnlyList<string> Commands => ["whoami", "me"];

    public string Usage => string.Empty;

    public string Description => "Show a summary of your own character.";

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var character = playerState.Character;
        if (character is null)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(character.Title) ? "no title" : $"\"{character.Title}\"";
        var gold = inventoryService.GetItemAmount(character, GoldItemId);

        await replies.WhisperAsync(playerState,
            $"{character.Name} - {title} - level {character.Level} {ClassName(character.Class)} ({character.Admin}).");

        await replies.WhisperAsync(playerState,
            $"Location: map {character.Map} ({playerState.CurrentMap?.Data?.Name ?? "Unknown"}) at ({character.X}, {character.Y}).");

        await replies.WhisperAsync(playerState,
            $"Vitals: {character.Hp}/{character.MaxHp} hp, {character.Tp}/{character.MaxTp} tp, {character.Sp}/{character.MaxSp} sp.");

        await replies.WhisperAsync(playerState,
            $"Carrying {Number(gold)} gold - {Number(character.StatPoints)} stat and {Number(character.SkillPoints)} skill points unspent, karma {character.Karma}.");
    }

    private string ClassName(int classId)
    {
        var classes = dataFiles.Ecf.Classes;
        return classId >= 1 && classId <= classes.Count
            ? classes[classId - 1].Name ?? $"Class {classId}"
            : $"Class {classId}";
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
