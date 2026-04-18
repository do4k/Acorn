namespace Acorn.Domain.Models;

/// <summary>
///     In-memory representation of a character's quest progress.
/// </summary>
public class CharacterQuestProgress
{
    public int QuestId { get; set; }
    public int State { get; set; }
    public Dictionary<int, int> NpcKills { get; set; } = new();
    public int PlayerKills { get; set; }
    public DateTime? DoneAt { get; set; }
    public int Completions { get; set; }

    public void AddNpcKill(int npcId)
    {
        if (NpcKills.ContainsKey(npcId))
            NpcKills[npcId]++;
        else
            NpcKills[npcId] = 1;
    }

    public int GetNpcKills(int npcId)
    {
        return NpcKills.GetValueOrDefault(npcId, 0);
    }
}
