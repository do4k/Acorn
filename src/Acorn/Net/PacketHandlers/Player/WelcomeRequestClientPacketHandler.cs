using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Infrastructure;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class WelcomeRequestClientPacketHandler : IPacketHandler<WelcomeRequestClientPacket>
{
    private readonly IDataFileRepository _dataRepository;
    private readonly IStatCalculator _statCalculator;
    private readonly IPaperdollService _paperdollService;
    private readonly ILogger<WelcomeRequestClientPacketHandler> _logger;

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

        _logger.LogInformation("Sending WelcomeReply with RIDs - ECF:{EcfRid} EIF:{EifRid} ENF:{EnfRid} ESF:{EsfRid} Map:{MapRid}",
            _dataRepository.Ecf.Rid, _dataRepository.Eif.Rid, _dataRepository.Enf.Rid, 
            _dataRepository.Esf.Rid, map.Rid);

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
                        Wis = playerState.Character.Wis
                    },
                    Secondary = new CharacterSecondaryStats
                    {
                        Accuracy = 10,
                        Armor = 10,
                        Evade = 10,
                        MaxDamage = 150,
                        MinDamage = 100
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

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (WelcomeRequestClientPacket)packet);
    }
}