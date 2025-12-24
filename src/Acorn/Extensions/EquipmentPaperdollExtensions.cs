using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Extensions;

public static class EquipmentPaperdollExtensions
{
    public static EquipmentWelcome AsEquipmentWelcome(this EquipmentPaperdoll paperdoll)
    {
        return new EquipmentWelcome
        {
            Accessory = paperdoll.Accessory,
            Armlet = paperdoll.Armlet,
            Armor = paperdoll.Armor,
            Belt = paperdoll.Belt,
            Boots = paperdoll.Boots,
            Bracer = paperdoll.Bracer,
            Gloves = paperdoll.Gloves,
            Hat = paperdoll.Hat,
            Necklace = paperdoll.Necklace,
            Ring = paperdoll.Ring,
            Shield = paperdoll.Shield,
            Weapon = paperdoll.Weapon
        };
    }
}