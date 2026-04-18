using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Acorn.Data;

public partial class QuestDataRepository : IQuestDataRepository
{
    private readonly Dictionary<int, QuestData> _quests = new();
    private readonly ILogger<QuestDataRepository> _logger;

    public IReadOnlyDictionary<int, QuestData> Quests => _quests;

    public QuestDataRepository(ILogger<QuestDataRepository> logger)
    {
        _logger = logger;
        LoadQuests();
    }

    public QuestData? GetQuest(int questId)
    {
        return _quests.GetValueOrDefault(questId);
    }

    private void LoadQuests()
    {
        var questDir = Path.Combine("Data", "quests");
        if (!Directory.Exists(questDir))
        {
            _logger.LogWarning("Quest directory not found: {Path}", questDir);
            return;
        }

        foreach (var file in Directory.GetFiles(questDir, "*.txt"))
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!int.TryParse(fileName, out var questId))
                {
                    _logger.LogWarning("Could not parse quest ID from filename: {File}", file);
                    continue;
                }

                var content = File.ReadAllText(file);
                var quest = ParseQuest(questId, content);
                if (quest != null)
                {
                    _quests[questId] = quest;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load quest file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} quests", _quests.Count);
    }

    private QuestData? ParseQuest(int id, string content)
    {
        // Remove comments
        var lines = content
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !l.TrimStart().StartsWith("//"))
            .ToList();

        var joined = string.Join("\n", lines);

        // Parse Main block
        var mainMatch = MainBlockRegex().Match(joined);
        if (!mainMatch.Success)
        {
            _logger.LogWarning("Quest {Id} has no Main block", id);
            return null;
        }

        var mainBody = mainMatch.Groups[1].Value;
        var nameMatch = QuestNameRegex().Match(mainBody);
        var versionMatch = VersionRegex().Match(mainBody);

        var name = nameMatch.Success ? nameMatch.Groups[1].Value : $"Quest {id}";
        var version = versionMatch.Success && int.TryParse(versionMatch.Groups[1].Value, out var v) ? v : 1;

        // Parse state blocks
        var states = new List<QuestState>();
        foreach (Match stateMatch in StateBlockRegex().Matches(joined))
        {
            var stateName = stateMatch.Groups[1].Value;
            var stateBody = stateMatch.Groups[2].Value;
            states.Add(ParseState(stateName, stateBody));
        }

        if (states.Count == 0)
        {
            _logger.LogWarning("Quest {Id} ({Name}) has no states", id, name);
            return null;
        }

        return new QuestData(id, name, version, states);
    }

    private static QuestState ParseState(string name, string body)
    {
        var description = "";
        var actions = new List<QuestAction>();
        var rules = new List<QuestRule>();

        var descMatch = DescRegex().Match(body);
        if (descMatch.Success)
        {
            description = descMatch.Groups[1].Value;
        }

        // Parse actions: action ActionName( args );
        // Also handle bare actions without "action" keyword (e.g., ShowHint("..."), Reset(), End())
        foreach (Match actionMatch in ActionRegex().Matches(body))
        {
            var actionName = actionMatch.Groups[1].Value;
            var argsStr = actionMatch.Groups[2].Value;
            actions.Add(new QuestAction(actionName, ParseArgs(argsStr)));
        }

        // Parse bare actions (not prefixed with "action") like End(), Reset(), ShowHint(...)
        foreach (Match bareMatch in BareActionRegex().Matches(body))
        {
            var actionName = bareMatch.Groups[1].Value;
            var argsStr = bareMatch.Groups[2].Value;
            actions.Add(new QuestAction(actionName, ParseArgs(argsStr)));
        }

        // Parse rules: rule RuleName( args ) goto StateName
        foreach (Match ruleMatch in RuleRegex().Matches(body))
        {
            var ruleName = ruleMatch.Groups[1].Value;
            var argsStr = ruleMatch.Groups[2].Value;
            var gotoState = ruleMatch.Groups[3].Value;
            rules.Add(new QuestRule(ruleName, ParseArgs(argsStr), gotoState));
        }

        return new QuestState(name, description, actions, rules);
    }

    private static List<QuestArg> ParseArgs(string argsStr)
    {
        var args = new List<QuestArg>();
        if (string.IsNullOrWhiteSpace(argsStr)) return args;

        var i = 0;
        while (i < argsStr.Length)
        {
            // Skip whitespace and commas
            while (i < argsStr.Length && (char.IsWhiteSpace(argsStr[i]) || argsStr[i] == ','))
                i++;

            if (i >= argsStr.Length) break;

            if (argsStr[i] == '"')
            {
                // String argument
                i++; // skip opening quote
                var start = i;
                while (i < argsStr.Length && argsStr[i] != '"')
                    i++;
                args.Add(new QuestArg.StrArg(argsStr[start..i]));
                if (i < argsStr.Length) i++; // skip closing quote
            }
            else if (char.IsDigit(argsStr[i]) || argsStr[i] == '-')
            {
                // Integer argument
                var start = i;
                if (argsStr[i] == '-') i++;
                while (i < argsStr.Length && char.IsDigit(argsStr[i]))
                    i++;
                if (int.TryParse(argsStr[start..i], out var val))
                    args.Add(new QuestArg.IntArg(val));
            }
            else
            {
                // Skip unknown chars
                i++;
            }
        }

        return args;
    }

    [GeneratedRegex(@"Main\s*\{([^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex MainBlockRegex();

    [GeneratedRegex(@"questname\s+""([^""]+)""")]
    private static partial Regex QuestNameRegex();

    [GeneratedRegex(@"version\s+(\d+)")]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"state\s+(\w+)\s*\{([^}]*(?:\{[^}]*\}[^}]*)*)\}", RegexOptions.Singleline)]
    private static partial Regex StateBlockRegex();

    [GeneratedRegex(@"desc\s+""([^""]+)""")]
    private static partial Regex DescRegex();

    [GeneratedRegex(@"action\s+(\w+)\s*\(([^)]*)\)\s*;?")]
    private static partial Regex ActionRegex();

    // Bare actions: lines starting with a function call that isn't prefixed by "action" or "rule"
    // Match: FunctionName( args ) or FunctionName() at the start of a trimmed line
    [GeneratedRegex(@"(?m)^\s+(?!action\b|rule\b|desc\b|questname\b|version\b)(\w+)\s*\(([^)]*)\)\s*;?$")]
    private static partial Regex BareActionRegex();

    [GeneratedRegex(@"rule\s+(\w+)\s*\(([^)]*)\)\s*goto\s+(\w+)")]
    private static partial Regex RuleRegex();
}
