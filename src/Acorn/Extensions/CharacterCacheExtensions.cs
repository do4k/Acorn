using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Shared.Caching;
using Acorn.Shared.Models.Online;

namespace Acorn.Extensions;

/// <summary>
/// Extension methods for caching character state changes in real-time.
/// </summary>
public static class CharacterCacheExtensions
{
    /// <summary>
    /// Cache a character's current state to Redis.
    /// Call this whenever a character's important data changes (position, level, stats, etc.)
    /// </summary>
    public static async Task CacheCharacterStateAsync(
        this PlayerState playerState,
        ICharacterCacheService characterCache,
        IPaperdollService paperdollService)
    {
        if (playerState.Character == null)
            return;

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
            SessionId = playerState.SessionId,
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

        await characterCache.CacheCharacterAsync(record);
    }
}
