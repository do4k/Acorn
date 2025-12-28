using Acorn.Shared.Models.Online;

namespace Acorn.Shared.Caching;

/// <summary>
/// Redis-based online character cache service.
/// </summary>
public class CharacterCacheService : ICharacterCacheService
{
    private readonly ICacheService _cache;

    private const string CharacterByNameKey = "online:character:name:";
    private const string CharacterBySessionKey = "online:character:session:";
    private const string OnlineListKey = "online:characters:all";

    public CharacterCacheService(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task CacheCharacterAsync(OnlineCharacterRecord character)
    {
        var expiry = TimeSpan.FromSeconds(30);

        // Cache by name and session ID for quick lookups
        await _cache.SetAsync($"{CharacterByNameKey}{character.Name.ToLowerInvariant()}", character, expiry);
        await _cache.SetAsync($"{CharacterBySessionKey}{character.SessionId}", character, expiry);

        // Update online list
        var onlineList = await _cache.GetAsync<List<string>>(OnlineListKey) ?? [];
        var nameLower = character.Name.ToLowerInvariant();
        if (!onlineList.Contains(nameLower))
        {
            onlineList.Add(nameLower);
            await _cache.SetAsync(OnlineListKey, onlineList, expiry);
        }
    }

    public async Task<OnlineCharacterRecord?> GetCharacterByNameAsync(string name)
    {
        return await _cache.GetAsync<OnlineCharacterRecord>($"{CharacterByNameKey}{name.ToLowerInvariant()}");
    }

    public async Task<OnlineCharacterRecord?> GetCharacterBySessionIdAsync(int sessionId)
    {
        return await _cache.GetAsync<OnlineCharacterRecord>($"{CharacterBySessionKey}{sessionId}");
    }

    public async Task<IReadOnlyList<OnlineCharacterRecord>> GetAllOnlineCharactersAsync()
    {
        var onlineList = await _cache.GetAsync<List<string>>(OnlineListKey) ?? [];
        var results = new List<OnlineCharacterRecord>();

        foreach (var name in onlineList)
        {
            var character = await GetCharacterByNameAsync(name);
            if (character != null)
            {
                results.Add(character);
            }
        }

        return results;
    }

    public async Task<OnlinePlayersRecord> GetOnlinePlayersAsync()
    {
        var characters = await GetAllOnlineCharactersAsync();
        return new OnlinePlayersRecord
        {
            TotalOnline = characters.Count,
            Players = characters.Select(c => new OnlinePlayerSummary
            {
                Name = c.Name,
                Level = c.Level,
                Class = c.Class.ToString(),
                MapId = c.MapId
            }).ToList()
        };
    }

    public async Task RemoveCharacterAsync(string name)
    {
        var character = await GetCharacterByNameAsync(name);
        if (character != null)
        {
            await _cache.RemoveAsync($"{CharacterByNameKey}{name.ToLowerInvariant()}");
            await _cache.RemoveAsync($"{CharacterBySessionKey}{character.SessionId}");

            var onlineList = await _cache.GetAsync<List<string>>(OnlineListKey) ?? [];
            onlineList.Remove(name.ToLowerInvariant());
            await _cache.SetAsync(OnlineListKey, onlineList);
        }
    }

    public async Task RemoveCharacterBySessionIdAsync(int sessionId)
    {
        var character = await GetCharacterBySessionIdAsync(sessionId);
        if (character != null)
        {
            await RemoveCharacterAsync(character.Name);
        }
    }
}

