using Acorn.Database.Models;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Extensions;

public static class CharacterExtensions
{
    public static CharacterSelectionListEntry AsCharacterListEntry(this Character c, int id)
    {
        return new CharacterSelectionListEntry
        {
            Admin = c.Admin,
            Equipment = new EquipmentCharacterSelect(),
            Gender = c.Gender,
            HairColor = c.HairColor,
            HairStyle = c.HairStyle,
            Level = c.Level,
            Name = c.Name,
            Skin = c.Race,
            Id = id
        };
    }
}