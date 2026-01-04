using Acorn.Database.Repository;
using Acorn.Game.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Game.Services;

public class PaperdollService : IPaperdollService
{
    private readonly IDataFileRepository _dataFiles;
    private readonly ILogger<PaperdollService> _logger;

    public PaperdollService(IDataFileRepository dataFiles, ILogger<PaperdollService> logger)
    {
        _dataFiles = dataFiles;
        _logger = logger;
    }

    public EquipmentWelcome ToEquipmentWelcome(EquipmentPaperdoll paperdoll)
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

    public EquipmentChange ToEquipmentChange(EquipmentPaperdoll paperdoll)
    {
        return new EquipmentChange
        {
            Armor = GetGraphicId(paperdoll.Armor),
            Boots = GetGraphicId(paperdoll.Boots),
            Hat = GetGraphicId(paperdoll.Hat),
            Shield = GetGraphicId(paperdoll.Shield),
            Weapon = GetGraphicId(paperdoll.Weapon)
        };
    }

    public EquipmentCharacterSelect ToEquipmentCharacterSelect(Character character)
    {
        return new EquipmentCharacterSelect
        {
            Armor = character.Paperdoll.Armor,
            Boots = character.Paperdoll.Boots,
            Weapon = character.Paperdoll.Weapon,
            Shield = character.Paperdoll.Shield,
            Hat = character.Paperdoll.Hat
        };
    }

    public EquipmentPaperdoll ToEquipmentPaperdoll(EquipmentPaperdoll paperdoll)
    {
        return new EquipmentPaperdoll
        {
            Hat = GetGraphicId(paperdoll.Hat),
            Necklace = GetGraphicId(paperdoll.Necklace),
            Armor = GetGraphicId(paperdoll.Armor),
            Belt = GetGraphicId(paperdoll.Belt),
            Boots = GetGraphicId(paperdoll.Boots),
            Gloves = GetGraphicId(paperdoll.Gloves),
            Weapon = GetGraphicId(paperdoll.Weapon),
            Shield = GetGraphicId(paperdoll.Shield),
            Accessory = GetGraphicId(paperdoll.Accessory),
            Ring = [GetGraphicId(paperdoll.Ring[0]), GetGraphicId(paperdoll.Ring[1])],
            Bracer = [GetGraphicId(paperdoll.Bracer[0]), GetGraphicId(paperdoll.Bracer[1])],
            Armlet = [GetGraphicId(paperdoll.Armlet[0]), GetGraphicId(paperdoll.Armlet[1])]
        };
    }

    private int GetGraphicId(int itemId)
    {
        if (itemId == 0) return 0;
        
        var item = _dataFiles.Eif.GetItem(itemId);
        var graphicId = item?.Spec1 ?? 0;
        
        _logger.LogDebug("GetGraphicId: ItemId={ItemId} -> GraphicId={GraphicId} (Item={ItemName})", 
            itemId, graphicId, item?.Name ?? "NOT FOUND");
        
        return graphicId;
    }
}
