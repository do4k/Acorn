using Acorn.World.Bot;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Extensions;

public static class ArenaBotExtensions
{
    /// <summary>
    ///     Converts a bot to CharacterMapInfo so it appears as a player on the map.
    /// </summary>
    public static CharacterMapInfo AsCharacterMapInfo(this ArenaBotState bot, WarpEffect warpEffect = WarpEffect.None)
    {
        // Create a bot with randomized appearance
        var info = new CharacterMapInfo
        {
            Name = bot.Name,
            PlayerId = bot.Id,
            MapId = bot.MapId,
            Coords = new BigCoords { X = bot.X, Y = bot.Y },
            Direction = bot.Direction,
            ClassId = 0, // Peasant
            Gender = Random.Shared.Next(2) == 0 ? Gender.Male : Gender.Female,
            HairStyle = (byte)Random.Shared.Next(1, 21), // Hair styles 1-20
            HairColor = (byte)Random.Shared.Next(0, 10), // Hair colors 0-9
            Skin = (byte)Random.Shared.Next(0, 6), // Skin tones 0-5
            Level = 1,
            Hp = 100,
            MaxHp = 100,
            Tp = 0,
            MaxTp = 0,
            Equipment = new EquipmentMapInfo
            {
                Boots = 0,
                Armor = 0,
                Hat = 0,
                Shield = 0,
                Weapon = 0
            },
            SitState = SitState.Stand,
            Invisible = false,
            WarpEffect = warpEffect,
            GuildTag = "BOT" // Make them clearly identifiable
        };
        
        return info;
    }
}
