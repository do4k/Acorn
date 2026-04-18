using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Telemetry;

/// <summary>
/// Compiled log messages using the [LoggerMessage] source generator for zero-allocation logging.
/// These cover critical server lifecycle and game events.
/// </summary>
public static partial class Log
{
    // --- Server lifecycle ---

    [LoggerMessage(Level = LogLevel.Information, Message = "World tick service started (tick rate: {TickRateMs}ms)")]
    public static partial void WorldTickStarted(this ILogger logger, double tickRateMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "World tick service stopped")]
    public static partial void WorldTickStopped(this ILogger logger);

    // --- Connection lifecycle ---

    [LoggerMessage(Level = LogLevel.Information, Message = "Player connected (Session {SessionId}) from {Origin}")]
    public static partial void PlayerConnected(this ILogger logger, int sessionId, string origin);

    [LoggerMessage(Level = LogLevel.Information, Message = "Player disconnected (Session {SessionId}, Account: {Account}, Character: {Character}): {Reason}")]
    public static partial void PlayerDisconnected(this ILogger logger, int sessionId, string? account, string? character, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Player {Character} (Session {SessionId}) entered the game world on map {MapId}")]
    public static partial void PlayerEnteredWorld(this ILogger logger, string character, int sessionId, int mapId);

    // --- Account & character ---

    [LoggerMessage(Level = LogLevel.Information, Message = "Account created: {Username}")]
    public static partial void AccountCreated(this ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Character '{CharacterName}' created by account '{Username}'")]
    public static partial void CharacterCreated(this ILogger logger, string characterName, string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful: {Username} (Session {SessionId})")]
    public static partial void LoginSuccessful(this ILogger logger, string username, int sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed for {Username}: {Reason}")]
    public static partial void LoginFailed(this ILogger logger, string username, string reason);

    // --- Combat ---

    [LoggerMessage(Level = LogLevel.Debug, Message = "NPC {NpcName} (ID {NpcId}) killed by {PlayerName} on map {MapId}")]
    public static partial void NpcKilled(this ILogger logger, string npcName, int npcId, string playerName, int mapId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Player {PlayerName} gained {Exp} exp (total: {TotalExp}, level: {Level})")]
    public static partial void ExperienceGained(this ILogger logger, string playerName, int exp, int totalExp, int level);

    [LoggerMessage(Level = LogLevel.Information, Message = "Player {PlayerName} leveled up to {Level}")]
    public static partial void PlayerLeveledUp(this ILogger logger, string playerName, int level);

    // --- Economy ---

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trade completed between {Player1} and {Player2}")]
    public static partial void TradeCompleted(this ILogger logger, string player1, string player2);

    // --- Networking ---

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to add player (Session {SessionId}) to world state")]
    public static partial void PlayerAddFailed(this ILogger logger, int sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rate limited packet {Action}_{Family} from session {SessionId}")]
    public static partial void PacketRateLimited(this ILogger logger, object action, object family, int sessionId);
}
