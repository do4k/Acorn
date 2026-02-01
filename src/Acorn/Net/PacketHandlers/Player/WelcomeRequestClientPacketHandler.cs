using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class WelcomeRequestClientPacketHandler : IPacketHandler<WelcomeRequestClientPacket>
{
    private readonly IDataFileRepository _dataRepository;
    private readonly ILogger<WelcomeRequestClientPacketHandler> _logger;
    private readonly IPaperdollService _paperdollService;
    private readonly IStatCalculator _statCalculator;

    public WelcomeRequestClientPacketHandler(
        IDataFileRepository dataRepository,
        IStatCalculator statCalculator,
        IPaperdollService paperdollService,
        ILogger<WelcomeRequestClientPacketHandler> logger
    )
    {
        _dataRepository = dataRepository;
        _statCalculator = statCalculator;
        _paperdollService = paperdollService;
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState,
        WelcomeRequestClientPacket packet)
    {
        var character = playerState.Account?.Characters[packet.CharacterId];
        if (character is null)
        {
            _logger.LogError("Could not find character");
            return;
        }

        //playerConnection.SessionId = _sessionGenerator.Generate();
        var map = _dataRepository.Maps.FirstOrDefault(map => map.Id == character.Map)?.Map;
        if (map is null)
        {
            _logger.LogError("Could not find map {MapId} for character {Name}", character.Map, character.Name);
            return;
        }

        playerState.Character = character.AsGameModel();
        var equipmentResult = playerState.Character.Equipment();
        _statCalculator.RecalculateStats(playerState.Character, _dataRepository.Ecf);

        await playerState.Send(new WelcomeReplyServerPacket
        {
            WelcomeCode = WelcomeCode.SelectCharacter,
            WelcomeCodeData = new WelcomeReplyServerPacket.WelcomeCodeDataSelectCharacter
            {
                Admin = playerState.Character.Admin,
                CharacterId = packet.CharacterId,
                ClassId = playerState.Character.Class,
                EcfLength = _dataRepository.Ecf.Classes.Count,
                EcfRid = _dataRepository.Ecf.Rid,
                EifLength = _dataRepository.Eif.Items.Count,
                EifRid = _dataRepository.Eif.Rid,
                EnfLength = _dataRepository.Enf.Npcs.Count,
                EnfRid = _dataRepository.Enf.Rid,
                Equipment = equipmentResult.AsEquipmentWelcome(_paperdollService),
                EsfLength = _dataRepository.Esf.Skills.Count,
                EsfRid = _dataRepository.Esf.Rid,
                Experience = playerState.Character.Exp,
                GuildName = "DansArmy",
                GuildRank = 0,
                GuildRankName = "The Boss",
                GuildTag = "DAN",
                MapFileSize = map.ByteSize,
                MapId = playerState.Character.Map,
                MapRid = map.Rid,
                Name = playerState.Character.Name,
                Stats = new CharacterStatsWelcome
                {
                    Base = new CharacterBaseStatsWelcome
                    {
                        Agi = playerState.Character.Agi,
                        Cha = playerState.Character.Cha,
                        Con = playerState.Character.Con,
                        Str = playerState.Character.Str,
                        Wis = playerState.Character.Wis,
                        Intl = playerState.Character.Int
                    },
                    Secondary = new CharacterSecondaryStats
                    {
                        Accuracy = playerState.Character.Accuracy,
                        Armor = playerState.Character.Armor,
                        Evade = playerState.Character.Evade,
                        MaxDamage = playerState.Character.MaxDamage,
                        MinDamage = playerState.Character.MinDamage
                    },
                    Karma = playerState.Character.Karma,
                    MaxSp = playerState.Character.MaxSp,
                    MaxTp = playerState.Character.MaxTp,
                    Tp = playerState.Character.Tp,
                    MaxHp = playerState.Character.MaxHp,
                    Hp = playerState.Character.Hp,
                    SkillPoints = playerState.Character.SkillPoints,
                    StatPoints = playerState.Character.StatPoints
                },
                Title = playerState.Character.Title ?? "",
                Usage = playerState.Character.Usage,
                SessionId = playerState.SessionId,
                Level = playerState.Character.Level,
                LoginMessageCode = playerState.Character.Usage switch
                {
                    0 => LoginMessageCode.Yes,
                    _ => LoginMessageCode.No
                },
                Settings = new ServerSettings
                {
                    JailMap = 2,
                    RescueMap = 4,
                    RescueCoords = new Coords { X = 24, Y = 24 },
                    SpyAndLightGuideFloodRate = 10,
                    GuardianFloodRate = 10,
                    GameMasterFloodRate = 10,
                    HighGameMasterFloodRate = 0
                }
            }
        });
    }

}