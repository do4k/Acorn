using Acorn.Shared.Caching;
using Acorn.Shared.Models.Online;
using Acorn.Shared.Options;
using Acorn.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Infrastructure;

/// <summary>
/// Hosted service that periodically caches online character data to Redis.
/// </summary>
public class CharacterCacheHostedService : BackgroundService
{
    private readonly WorldState _worldState;
    private readonly ICharacterCacheService _characterCache;
    private readonly ILogger<CharacterCacheHostedService> _logger;
    private readonly CacheOptions _cacheOptions;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5);

    public CharacterCacheHostedService(
        WorldState worldState,
        ICharacterCacheService characterCache,
        IOptions<CacheOptions> cacheOptions,
        ILogger<CharacterCacheHostedService> logger)
    {
        _worldState = worldState;
        _characterCache = characterCache;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Character cache service started, updating every {Interval} seconds", _updateInterval.TotalSeconds);

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
                    continue;

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
                    GuildName = string.Empty, // TODO: Add guild support
                    GuildRank = string.Empty,
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

        if (_cacheOptions.LogOperations)
            _logger.LogDebug("Cached {Count} online characters to Redis", cachedCount);
    }
}

