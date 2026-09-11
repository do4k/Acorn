using Acorn.Data;
using Acorn.Game.Models;

namespace Acorn.World.Services.Quest;

/// <summary>
///     Evaluates a single quest state rule against a character's current state.
///     Kept pure/static so the rule engine can be unit tested without a live player session.
/// </summary>
public static class QuestRuleEvaluator
{
    /// <summary>
    ///     Returns true when the given rule's condition is satisfied for the character.
    /// </summary>
    public static bool Evaluate(QuestRule rule, Character character, CharacterQuestProgress progress)
    {
        return rule.Name switch
        {
            "Always" => true,
            "GotItems" => HasItems(character, rule, atLeast: true),
            "LostItems" => HasItems(character, rule, atLeast: false),
            "KilledNpcs" => KilledNpcs(progress, rule),
            "KilledPlayers" => KilledPlayers(progress, rule),
            "EnterMap" => EnterMap(character, rule),
            "EnterCoord" => EnterCoord(character, rule),
            "LeaveMap" => LeaveMap(character, rule),
            "IsClass" => IsClass(character, rule),
            "IsGender" => IsGender(character, rule),
            "IsRace" => IsRace(character, rule),
            _ => false
        };
    }

    private static bool HasItems(Character character, QuestRule rule, bool atLeast)
    {
        if (rule.Args.Count == 0) return false;

        var itemId = rule.Args[0].AsInt();
        var required = rule.Args.Count >= 2 ? rule.Args[1].AsInt() : 1;
        var amount = character.Inventory.Items.FirstOrDefault(i => i.Id == itemId)?.Amount ?? 0;

        return atLeast ? amount >= required : amount < required;
    }

    private static bool KilledNpcs(CharacterQuestProgress progress, QuestRule rule)
    {
        if (rule.Args.Count == 0) return false;

        var npcId = rule.Args[0].AsInt();
        var required = rule.Args.Count >= 2 ? rule.Args[1].AsInt() : 1;

        return progress.GetNpcKills(npcId) >= required;
    }

    private static bool KilledPlayers(CharacterQuestProgress progress, QuestRule rule)
    {
        var required = rule.Args.Count >= 1 ? rule.Args[0].AsInt() : 1;
        return progress.PlayerKills >= required;
    }

    private static bool EnterMap(Character character, QuestRule rule)
    {
        return rule.Args.Count >= 1 && character.Map == rule.Args[0].AsInt();
    }

    private static bool EnterCoord(Character character, QuestRule rule)
    {
        return rule.Args.Count >= 3
               && character.Map == rule.Args[0].AsInt()
               && character.X == rule.Args[1].AsInt()
               && character.Y == rule.Args[2].AsInt();
    }

    private static bool LeaveMap(Character character, QuestRule rule)
    {
        return rule.Args.Count >= 1 && character.Map != rule.Args[0].AsInt();
    }

    private static bool IsClass(Character character, QuestRule rule)
    {
        return rule.Args.Count >= 1 && character.Class == rule.Args[0].AsInt();
    }

    private static bool IsGender(Character character, QuestRule rule)
    {
        return rule.Args.Count >= 1 && (int)character.Gender == rule.Args[0].AsInt();
    }

    private static bool IsRace(Character character, QuestRule rule)
    {
        return rule.Args.Count >= 1 && character.Race == rule.Args[0].AsInt();
    }
}
