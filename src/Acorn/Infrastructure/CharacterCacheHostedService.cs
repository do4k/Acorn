using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.Shared.Models.Online;
using Acorn.Shared.Options;
using Acorn.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Infrastructure;

/// <summary>
///     Hosted service that periodically caches online character data.
/// </summary>
public class CharacterCacheHostedService : BackgroundService
{
    /// <summary>
    ///     Sentinel session id for the synthetic Acornbot entry; player session ids
    ///     start at 1, so this can never collide.
    /// </summary>
    private const int AcornbotSessionId = -1;

    private readonly IOptions<AcornbotOptions> _acornbotOptions;
    private readonly CacheOptions _cacheOptions;
    private readonly ICharacterCacheService _characterCache;
    private readonly ILogger<CharacterCacheHostedService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5);
    private readonly WorldState _worldState;

    public CharacterCacheHostedService(
        WorldState worldState,
        ICharacterCacheService characterCache,
        IOptions<CacheOptions> cacheOptions,
        IOptions<AcornbotOptions> acornbotOptions,
        ILogger<CharacterCacheHostedService> logger)
    {
        _worldState = worldState;
        _characterCache = characterCache;
        _cacheOptions = cacheOptions.Value;
        _acornbotOptions = acornbotOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Character cache service started, updating every {Interval} seconds",
            _updateInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CacheAllCharactersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching character states");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }
    }

    private async Task CacheAllCharactersAsync()
    {
        var cachedCount = 0;

        foreach (var (sessionId, playerState) in _worldState.Players)
        {
            try
            {
                if (playerState.Character == null)
                {
                    continue;
                }

                var character = playerState.Character;
                var equipment = new EquipmentRecord
                {
                    Weapon = character.Paperdoll.Weapon,
                    Shield = character.Paperdoll.Shield,
                    Armor = character.Paperdoll.Armor,
                    Hat = character.Paperdoll.Hat,
                    Boots = character.Paperdoll.Boots,
                    Gloves = character.Paperdoll.Gloves,
                    Belt = character.Paperdoll.Belt,
                    Necklace = character.Paperdoll.Necklace,
                    Ring1 = character.Paperdoll.Ring1,
                    Ring2 = character.Paperdoll.Ring2,
                    Armlet1 = character.Paperdoll.Armlet1,
                    Armlet2 = character.Paperdoll.Armlet2,
                    Bracer1 = character.Paperdoll.Bracer1,
                    Bracer2 = character.Paperdoll.Bracer2
                };

                var record = new OnlineCharacterRecord
                {
                    SessionId = sessionId,
                    Name = character.Name ?? string.Empty,
                    Title = character.Title ?? string.Empty,
                    GuildName = character.GuildName ?? string.Empty,
                    GuildRank = character.GuildRankName ?? string.Empty,
                    Level = character.Level,
                    Class = character.Class,
                    Gender = character.Gender.ToString(),
                    Admin = character.Admin.ToString(),
                    MapId = character.Map,
                    X = character.X,
                    Y = character.Y,
                    Direction = character.Direction.ToString(),
                    Hp = character.Hp,
                    MaxHp = character.MaxHp,
                    Tp = character.Tp,
                    MaxTp = character.MaxTp,
                    Exp = character.Exp,
                    Equipment = equipment
                };

                await _characterCache.CacheCharacterAsync(record);
                cachedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache character for session {SessionId}", sessionId);
            }
        }

        // Advertise Acornbot in the online list so players can discover the whisper
        // command; its title is the hint. When the bot is disabled the cached entry
        // simply expires (30 s) instead of being refreshed.
        if (CreateAcornbotRecordIfEnabled(_acornbotOptions.Value) is { } bot)
        {
            await _characterCache.CacheCharacterAsync(bot);
        }

        if (_cacheOptions.LogOperations)
        {
            _logger.LogDebug("Cached {Count} online characters", cachedCount);
        }
    }

    internal static OnlineCharacterRecord? CreateAcornbotRecordIfEnabled(AcornbotOptions acornbot)
    {
        if (!acornbot.Enabled)
        {
            return null;
        }

        return new OnlineCharacterRecord
        {
            SessionId = AcornbotSessionId,
            Name = acornbot.Name,
            Title = AcornbotPresence.OnlineListTitle,
            Level = 1,
            Class = 0,
            Gender = "Bot",
            Admin = "Player"
        };
    }
}