namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     Short human-readable descriptions for the help command, keyed by prefix and
///     primary command name. Kept in one place so help output stays complete; a
///     test asserts every registered command has an entry.
/// </summary>
internal static class CommandDescriptions
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Admin commands
        ["$help"] = "List available commands or show a command's usage.",
        ["$warp"] = "Warp to a map by id or name.",
        ["$wmt"] = "Warp to another online player's location.",
        ["$summon"] = "Warp another online player to your location.",
        ["$who"] = "List online players with their map and coordinates.",
        ["$info"] = "Show a player's stats and location.",
        ["$inventory"] = "Show another player's inventory and bank.",
        ["$kick"] = "Disconnect a player.",
        ["$jail"] = "Warp a player to the jail map.",
        ["$free"] = "Release a player from jail.",
        ["$ban"] = "Ban a player's account and disconnect them.",
        ["$mute"] = "Temporarily mute a player's chat.",
        ["$unmute"] = "Remove a player's mute.",
        ["$freeze"] = "Freeze a player's movement.",
        ["$unfreeze"] = "Restore a player's movement.",
        ["$hide"] = "Toggle your visibility to other players.",
        ["$evacuate"] = "Warp everyone on your map to the home map.",
        ["$quake"] = "Trigger a screen quake.",
        ["$global"] = "Send a server-wide announcement.",
        ["$set"] = "Set a player attribute.",
        ["$spawnitem"] = "Add item(s) to your inventory.",
        ["$spawnnpc"] = "Spawn NPC(s) at your position.",
        ["$addspell"] = "Teach a spell to a character.",
        ["$repub"] = "Reload the pub data files and cache.",
        ["$rehash"] = "Re-read configuration and refresh pub files.",
        ["$uptime"] = "Show how long the server has been running.",
        ["$item"] = "Look up item data by id or name.",
        ["$npc"] = "Look up NPC data by id or name.",
        ["$spellinfo"] = "Look up spell data by id or name.",
        ["$class"] = "Look up class data by id or name.",
        ["$qstate"] = "Show a character's quest progress.",

        // Player commands
        ["#help"] = "List available commands or show a command's usage.",
        ["#loc"] = "Show your current map and coordinates.",
        ["#inventory"] = "Show how many items you are carrying.",
        ["#usage"] = "Show your total play time."
    };

    public static string? Get(char prefix, string command)
        => Descriptions.TryGetValue($"{prefix}{command}", out var description) ? description : null;
}
