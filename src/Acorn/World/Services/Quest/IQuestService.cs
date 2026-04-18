using Acorn.Net;

namespace Acorn.World.Services.Quest;

/// <summary>
///     Service responsible for quest dialog interactions and progress tracking.
/// </summary>
public interface IQuestService
{
    /// <summary>Initiate quest dialog with a quest NPC.</summary>
    Task TalkToQuestNpc(PlayerState player, int npcIndex, int questId);

    /// <summary>Handle a player's reply to a quest dialog.</summary>
    Task ReplyToQuestNpc(PlayerState player, int sessionId, int questId, int? actionId);

    /// <summary>View active quest progress list.</summary>
    Task ViewQuestProgress(PlayerState player);

    /// <summary>View completed quest history.</summary>
    Task ViewQuestHistory(PlayerState player);

    /// <summary>Load quest progress from DB for a character.</summary>
    Task LoadQuestProgress(string characterName, Game.Models.Character character);

    /// <summary>Save quest progress to DB for a character.</summary>
    Task SaveQuestProgress(Game.Models.Character character);
}
