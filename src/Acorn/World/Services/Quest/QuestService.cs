using System.Text.Json;
using Acorn.Data;
using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using GameCharacter = Acorn.Game.Models.Character;
using CharacterQuestProgress = Acorn.Game.Models.CharacterQuestProgress;

namespace Acorn.World.Services.Quest;

public class QuestService(
    IQuestDataRepository questDataRepository,
    IServiceScopeFactory scopeFactory,
    ILogger<QuestService> logger) : IQuestService
{
    public async Task TalkToQuestNpc(PlayerState player, int npcIndex, int questId)
    {
        var character = player.Character!;
        var map = player.CurrentMap!;

        if (!map.Npcs.TryGetValue(npcIndex, out var npc))
        {
            logger.LogWarning("Player {Character} tried to talk to invalid NPC index {NpcIndex}",
                character.Name, npcIndex);
            return;
        }

        if (npc.Data.Type != NpcType.Quest)
        {
            logger.LogDebug("Player {Character} tried to talk to non-quest NPC {NpcId}", character.Name, npc.Id);
            return;
        }

        var behaviorId = npc.Data.BehaviorId;

        // Find all quests that have dialog for this NPC behavior at their current state
        var questsForNpc = questDataRepository.Quests.Values
            .Where(q =>
            {
                var progress = GetOrCreateProgress(character, q.Id);
                if (progress.State >= q.States.Count) return false;
                var state = q.States[progress.State];
                return state.Actions.Any(a =>
                    (a.Name == "AddNpcText" || a.Name == "AddNpcInput") &&
                    a.Args.Count > 0 && a.Args[0].AsInt() == behaviorId);
            })
            .ToList();

        if (questsForNpc.Count == 0)
        {
            logger.LogDebug("No quests available for NPC behavior {BehaviorId}", behaviorId);
            return;
        }

        // Select the requested quest, or first available
        var quest = questId > 0
            ? questsForNpc.FirstOrDefault(q => q.Id == questId)
            : questsForNpc[0];

        if (quest == null) return;

        var currentProgress = GetOrCreateProgress(character, quest.Id);
        if (currentProgress.State >= quest.States.Count) return;

        var currentState = quest.States[currentProgress.State];

        // Build dialog entries
        var dialogEntries = BuildDialogEntries(currentState, behaviorId);

        // Build quest entries (all quests available at this NPC)
        var questEntries = questsForNpc
            .OrderBy(q => q.Id == quest.Id ? 0 : q.Id)
            .Select(q => new DialogQuestEntry { QuestId = q.Id, QuestName = q.Name })
            .ToList();

        // Set interaction state
        player.InteractingNpcIndex = npcIndex;

        // Generate a new session ID
        var sessionId = player.Rnd.Next(1, int.MaxValue);
        player.SessionId = sessionId;

        await player.Send(new QuestDialogServerPacket
        {
            BehaviorId = behaviorId,
            QuestId = quest.Id,
            SessionId = sessionId,
            DialogId = 0,
            QuestEntries = questEntries,
            DialogEntries = dialogEntries
        });

        // Send NpcChat messages nearby
        var chatMessages = currentState.Actions
            .Where(a => a.Name == "AddNpcChat" && a.Args.Count >= 2 && a.Args[0].AsInt() == behaviorId)
            .Select(a => a.Args[1].AsStr())
            .Where(m => !string.IsNullOrEmpty(m))
            .ToList();

        if (chatMessages.Count > 0)
        {
            var reportPacket = new QuestReportServerPacket
            {
                NpcIndex = npcIndex,
                Messages = chatMessages
            };

            // Send to all players on map
            foreach (var p in map.Players.Values)
            {
                await p.Send(reportPacket);
            }
        }

        logger.LogInformation("Player {Character} talking to quest NPC (quest {QuestId}: {QuestName})",
            character.Name, quest.Id, quest.Name);
    }

    public async Task ReplyToQuestNpc(PlayerState player, int sessionId, int questId, int? actionId)
    {
        var character = player.Character!;
        var map = player.CurrentMap!;

        // Validate session
        if (player.SessionId != sessionId)
        {
            logger.LogWarning("Player {Character} sent quest reply with invalid session", character.Name);
            return;
        }

        var npcIndex = player.InteractingNpcIndex;
        if (npcIndex == null) return;

        if (!map.Npcs.TryGetValue(npcIndex.Value, out var npc)) return;
        if (npc.Data.Type != NpcType.Quest) return;

        var behaviorId = npc.Data.BehaviorId;

        var quest = questDataRepository.GetQuest(questId);
        if (quest == null) return;

        var progress = GetOrCreateProgress(character, questId);
        var previousState = progress.State;

        // Process the NPC reply - advance quest state
        TalkedToNpc(character, behaviorId, questId, actionId);

        // Re-check which quests have dialog at this NPC after state change
        var questsForNpc = questDataRepository.Quests.Values
            .Where(q =>
            {
                var p = GetOrCreateProgress(character, q.Id);
                if (p.State >= q.States.Count) return false;
                var state = q.States[p.State];
                return state.Actions.Any(a =>
                    (a.Name == "AddNpcText" || a.Name == "AddNpcInput") &&
                    a.Args.Count > 0 && a.Args[0].AsInt() == behaviorId);
            })
            .ToList();

        if (questsForNpc.Count == 0) return;

        // Refresh progress after state change
        progress = GetOrCreateProgress(character, questId);

        // Only show new dialog if state advanced
        if (previousState >= progress.State) return;
        if (progress.State >= quest.States.Count) return;

        var currentState = quest.States[progress.State];
        var dialogEntries = BuildDialogEntries(currentState, behaviorId);

        var questEntries = questsForNpc
            .OrderBy(q => q.Id == questId ? 0 : q.Id)
            .Select(q => new DialogQuestEntry { QuestId = q.Id, QuestName = q.Name })
            .ToList();

        // Generate new session
        var newSessionId = player.Rnd.Next(1, int.MaxValue);
        player.SessionId = newSessionId;

        await player.Send(new QuestDialogServerPacket
        {
            BehaviorId = behaviorId,
            QuestId = questId,
            SessionId = newSessionId,
            DialogId = 0,
            QuestEntries = questEntries,
            DialogEntries = dialogEntries
        });

        // Send NpcChat messages
        var chatMessages = currentState.Actions
            .Where(a => a.Name == "AddNpcChat" && a.Args.Count >= 2 && a.Args[0].AsInt() == behaviorId)
            .Select(a => a.Args[1].AsStr())
            .Where(m => !string.IsNullOrEmpty(m))
            .ToList();

        if (chatMessages.Count > 0)
        {
            var reportPacket = new QuestReportServerPacket
            {
                NpcIndex = npcIndex.Value,
                Messages = chatMessages
            };

            foreach (var p in map.Players.Values)
            {
                await p.Send(reportPacket);
            }
        }
    }

    public async Task ViewQuestProgress(PlayerState player)
    {
        var character = player.Character!;

        var progressEntries = character.Quests
            .Where(q => q.DoneAt == null || q.State == 0)
            .Select(q =>
            {
                var quest = questDataRepository.GetQuest(q.QuestId);
                if (quest == null) return null;
                if (q.State >= quest.States.Count) return null;

                var state = quest.States[q.State];

                // Determine icon and progress based on current state rules
                var (icon, progress, target) = DetermineProgressInfo(state, q, character);

                return new QuestProgressEntry
                {
                    Name = quest.Name,
                    Description = state.Description,
                    Icon = icon,
                    Progress = progress,
                    Target = target
                };
            })
            .Where(e => e != null)
            .Cast<QuestProgressEntry>()
            .ToList();

        await player.Send(new QuestListServerPacket
        {
            Page = QuestPage.Progress,
            QuestsCount = progressEntries.Count,
            PageData = new QuestListServerPacket.PageDataProgress
            {
                QuestProgressEntries = progressEntries
            }
        });
    }

    public async Task ViewQuestHistory(PlayerState player)
    {
        var character = player.Character!;

        var completedQuests = character.Quests
            .Where(q => q.DoneAt != null && q.State != 0)
            .Select(q => questDataRepository.GetQuest(q.QuestId)?.Name)
            .Where(name => name != null)
            .Cast<string>()
            .ToList();

        await player.Send(new QuestListServerPacket
        {
            Page = QuestPage.History,
            QuestsCount = completedQuests.Count,
            PageData = new QuestListServerPacket.PageDataHistory
            {
                CompletedQuests = completedQuests
            }
        });
    }

    public async Task LoadQuestProgress(string characterName, GameCharacter character)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var dbProgress = await db.QuestProgress
            .Where(q => q.CharacterName == characterName)
            .ToListAsync();

        character.Quests = dbProgress.Select(q => new CharacterQuestProgress
        {
            QuestId = q.QuestId,
            State = q.State,
            NpcKills = DeserializeNpcKills(q.NpcKillsJson),
            PlayerKills = q.PlayerKills,
            DoneAt = q.DoneAt,
            Completions = q.Completions
        }).ToList();

        logger.LogDebug("Loaded {Count} quest progress entries for {Character}", character.Quests.Count, characterName);
    }

    public async Task SaveQuestProgress(GameCharacter character)
    {
        if (character.Name == null) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var existing = await db.QuestProgress
            .Where(q => q.CharacterName == character.Name)
            .ToListAsync();

        // Remove quests no longer tracked
        var toRemove = existing.Where(e => !character.Quests.Any(q => q.QuestId == e.QuestId)).ToList();
        db.QuestProgress.RemoveRange(toRemove);

        foreach (var quest in character.Quests)
        {
            var existingEntry = existing.FirstOrDefault(e => e.QuestId == quest.QuestId);
            if (existingEntry != null)
            {
                existingEntry.State = quest.State;
                existingEntry.NpcKillsJson = SerializeNpcKills(quest.NpcKills);
                existingEntry.PlayerKills = quest.PlayerKills;
                existingEntry.DoneAt = quest.DoneAt;
                existingEntry.Completions = quest.Completions;
            }
            else
            {
                db.QuestProgress.Add(new Database.Models.QuestProgress
                {
                    CharacterName = character.Name,
                    QuestId = quest.QuestId,
                    State = quest.State,
                    NpcKillsJson = SerializeNpcKills(quest.NpcKills),
                    PlayerKills = quest.PlayerKills,
                    DoneAt = quest.DoneAt,
                    Completions = quest.Completions
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private void TalkedToNpc(GameCharacter character, int behaviorId, int questId, int? actionId)
    {
        var progress = GetOrCreateProgress(character, questId);
        var quest = questDataRepository.GetQuest(questId);
        if (quest == null) return;
        if (progress.State >= quest.States.Count) return;

        var state = quest.States[progress.State];

        // Find matching rule
        QuestRule? matchedRule = null;
        foreach (var rule in state.Rules)
        {
            if (actionId.HasValue)
            {
                if (rule.Name == "InputNpc" && rule.Args.Count > 0 && rule.Args[0].AsInt() == actionId.Value)
                {
                    matchedRule = rule;
                    break;
                }
            }
            else
            {
                if (rule.Name == "TalkedToNpc" && rule.Args.Count > 0 && rule.Args[0].AsInt() == behaviorId)
                {
                    matchedRule = rule;
                    break;
                }
            }
        }

        if (matchedRule == null) return;

        // Find the target state index
        var nextStateIndex = quest.States.FindIndex(s => s.Name == matchedRule.Goto);
        if (nextStateIndex < 0) return;

        progress.State = nextStateIndex;

        // Execute state actions
        DoQuestActions(character, questId);
    }

    private void DoQuestActions(GameCharacter character, int questId)
    {
        var progress = character.Quests.FirstOrDefault(q => q.QuestId == questId);
        if (progress == null) return;

        var quest = questDataRepository.GetQuest(questId);
        if (quest == null || progress.State >= quest.States.Count) return;

        var state = quest.States[progress.State];

        foreach (var action in state.Actions)
        {
            switch (action.Name)
            {
                case "AddNpcText":
                case "AddNpcChat":
                case "AddNpcInput":
                    // These are dialog actions, handled during dialog building
                    break;

                case "End":
                    progress.DoneAt = DateTime.UtcNow;
                    break;

                case "ResetDaily":
                    if (progress.DoneAt == null)
                        progress.DoneAt = DateTime.UtcNow;
                    progress.Completions++;
                    progress.State = 0;
                    break;

                case "Reset":
                    if (progress.DoneAt == null)
                    {
                        character.Quests.Remove(progress);
                        return; // Progress removed, stop processing
                    }
                    progress.State = 0;
                    break;

                case "GiveExp":
                    if (action.Args.Count > 0)
                    {
                        var amount = action.Args[0].AsInt();
                        character.Exp += amount;
                        // Level up logic would go here - simplified for now
                        logger.LogInformation("Quest {QuestId} awarded {Amount} EXP to {Character}",
                            questId, amount, character.Name);
                    }
                    break;

                case "ShowHint":
                    // ShowHint is sent as a server message - handled via quest actions
                    // For now we just log; full implementation would send MessageOpenServerPacket
                    break;

                case "PlaySound":
                case "PlayMusic":
                case "SetMap":
                case "GiveItem":
                case "RemoveItem":
                case "SetClass":
                case "GiveKarma":
                case "RemoveKarma":
                    // These actions require player context and will be handled separately
                    // via packet sending in the handler layer
                    break;
            }
        }

        // Check "Always" rule for automatic state transitions
        var alwaysRule = state.Rules.FirstOrDefault(r => r.Name == "Always");
        if (alwaysRule != null)
        {
            var nextStateIndex = quest.States.FindIndex(s => s.Name == alwaysRule.Goto);
            if (nextStateIndex >= 0)
            {
                progress.State = nextStateIndex;
                DoQuestActions(character, questId); // Recurse
            }
        }

        // Check "GotItems" rule
        var gotItemsRule = state.Rules.FirstOrDefault(r => r.Name == "GotItems");
        if (gotItemsRule != null && gotItemsRule.Args.Count >= 2)
        {
            var itemId = gotItemsRule.Args[0].AsInt();
            var requiredAmount = gotItemsRule.Args[1].AsInt();
            var playerAmount = character.Inventory.Items
                .FirstOrDefault(i => i.Id == itemId)?.Amount ?? 0;

            if (playerAmount >= requiredAmount)
            {
                var nextStateIndex = quest.States.FindIndex(s => s.Name == gotItemsRule.Goto);
                if (nextStateIndex >= 0)
                {
                    progress.State = nextStateIndex;
                    DoQuestActions(character, questId); // Recurse
                }
            }
        }
    }

    private static CharacterQuestProgress GetOrCreateProgress(GameCharacter character, int questId)
    {
        var existing = character.Quests.FirstOrDefault(q => q.QuestId == questId);
        if (existing != null) return existing;

        var newProgress = new CharacterQuestProgress { QuestId = questId, State = 0 };
        character.Quests.Add(newProgress);
        return newProgress;
    }

    private static List<DialogEntry> BuildDialogEntries(QuestState state, int behaviorId)
    {
        return state.Actions
            .Where(a => (a.Name == "AddNpcText" || a.Name == "AddNpcInput") &&
                        a.Args.Count > 0 && a.Args[0].AsInt() == behaviorId)
            .Select(a =>
            {
                if (a.Name == "AddNpcText" && a.Args.Count >= 2)
                {
                    return new DialogEntry
                    {
                        EntryType = DialogEntryType.Text,
                        Line = a.Args[1].AsStr()
                    };
                }

                if (a.Name == "AddNpcInput" && a.Args.Count >= 3)
                {
                    return new DialogEntry
                    {
                        EntryType = DialogEntryType.Link,
                        EntryTypeData = new DialogEntry.EntryTypeDataLink
                        {
                            LinkId = a.Args[1].AsInt()
                        },
                        Line = a.Args[2].AsStr()
                    };
                }

                return null;
            })
            .Where(e => e != null)
            .Cast<DialogEntry>()
            .ToList();
    }

    private static (QuestRequirementIcon icon, int progress, int target) DetermineProgressInfo(
        QuestState state, CharacterQuestProgress q, GameCharacter character)
    {
        // Check for GotItems rule
        var gotItemsRule = state.Rules.FirstOrDefault(r => r.Name == "GotItems");
        if (gotItemsRule != null && gotItemsRule.Args.Count >= 2)
        {
            var itemId = gotItemsRule.Args[0].AsInt();
            var amount = gotItemsRule.Args[1].AsInt();
            var playerAmount = character.Inventory.Items
                .FirstOrDefault(i => i.Id == itemId)?.Amount ?? 0;
            return (QuestRequirementIcon.Item, playerAmount, amount);
        }

        // Check for EquippedItem/UnequippedItem rules
        if (state.Rules.Any(r => r.Name is "EquippedItem" or "UnequippedItem"))
        {
            return (QuestRequirementIcon.Item, 0, 1);
        }

        // Check for KilledNpcs rule
        var killedNpcsRule = state.Rules.FirstOrDefault(r => r.Name == "KilledNpcs");
        if (killedNpcsRule != null && killedNpcsRule.Args.Count >= 2)
        {
            var npcId = killedNpcsRule.Args[0].AsInt();
            var amount = killedNpcsRule.Args[1].AsInt();
            return (QuestRequirementIcon.Kill, q.GetNpcKills(npcId), amount);
        }

        // Check for KilledPlayers rule
        var killedPlayersRule = state.Rules.FirstOrDefault(r => r.Name == "KilledPlayers");
        if (killedPlayersRule != null && killedPlayersRule.Args.Count >= 1)
        {
            var amount = killedPlayersRule.Args[0].AsInt();
            return (QuestRequirementIcon.Kill, q.PlayerKills, amount);
        }

        // Check for map/coord rules
        if (state.Rules.Any(r => r.Name is "EnterCoord" or "EnterMap" or "LeaveMap"))
        {
            return (QuestRequirementIcon.Step, 0, 0);
        }

        // Default: talk
        return (QuestRequirementIcon.Talk, 0, 0);
    }

    private static string SerializeNpcKills(Dictionary<int, int> npcKills)
    {
        return JsonSerializer.Serialize(npcKills);
    }

    private static Dictionary<int, int> DeserializeNpcKills(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new Dictionary<int, int>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
        }
        catch
        {
            return new Dictionary<int, int>();
        }
    }
}
