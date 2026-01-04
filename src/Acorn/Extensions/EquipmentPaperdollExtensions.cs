using Acorn.Game.Services;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Extensions;

public static class EquipmentPaperdollExtensions
{
    public static EquipmentWelcome AsEquipmentWelcome(this EquipmentPaperdoll paperdoll, IPaperdollService paperdollService)
        => paperdollService.ToEquipmentWelcome(paperdoll);
    
    public static EquipmentChange AsEquipmentChange(this EquipmentPaperdoll paperdoll, IPaperdollService paperdollService)
        => paperdollService.ToEquipmentChange(paperdoll);
}