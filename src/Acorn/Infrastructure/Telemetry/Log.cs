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

    // --- Data loading ---

    [LoggerMessage(Level = LogLevel.Warning, Message = "Data directory not found at {Directory}, creating with sample")]
    public static partial void DataDirectoryNotFound(this ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create data directory at {Directory} (read-only filesystem?), continuing without data")]
    public static partial void DataDirectoryCreateFailed(this ILogger logger, Exception exception, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No data files found in {Directory}, creating sample")]
    public static partial void DataDirectoryEmpty(this ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse data file: {File}")]
    public static partial void DataFileParseFailed(this ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error loading data file: {File}")]
    public static partial void DataFileLoadError(this ILogger logger, Exception exception, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created sample data file at {Path}")]
    public static partial void SampleDataFileCreated(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded skill master: {Name} (BehaviorId: {BehaviorId}, {SkillCount} skills)")]
    public static partial void SkillMasterLoaded(this ILogger logger, string name, int behaviorId, int skillCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} skill masters")]
    public static partial void SkillMastersLoaded(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded shop: {Name} (BehaviorId: {BehaviorId}, {TradeCount} trades, {CraftCount} crafts)")]
    public static partial void ShopLoaded(this ILogger logger, string name, int behaviorId, int tradeCount, int craftCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} shops")]
    public static partial void ShopsLoaded(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded inn: {Name} (BehaviorId: {BehaviorId})")]
    public static partial void InnLoaded(this ILogger logger, string name, int behaviorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} inns")]
    public static partial void InnsLoaded(this ILogger logger, int count);
}
