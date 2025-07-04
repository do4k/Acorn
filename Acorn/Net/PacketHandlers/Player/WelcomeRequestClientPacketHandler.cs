using Acorn.Database.Repository;
using Acorn.Extensions;
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
    private readonly ILogger<WelcomeRequestClientPacketHandler> _logger;

    public WelcomeRequestClientPacketHandler(
        IDataFileRepository dataRepository,
        ILogger<WelcomeRequestClientPacketHandler> logger
    )
    {
        _dataRepository = dataRepository;
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

        var equipmentResult = character.Equipment();
        playerState.Character = character;
        character.CalculateStats(_dataRepository.Ecf);

        await playerState.Send(new WelcomeReplyServerPacket
        {
            WelcomeCode = WelcomeCode.SelectCharacter,
            WelcomeCodeData = new WelcomeReplyServerPacket.WelcomeCodeDataSelectCharacter
            {
                Admin = character.Admin,
                CharacterId = packet.CharacterId,
                ClassId = character.Class,
                EcfLength = _dataRepository.Ecf.ByteSize,
                EcfRid = _dataRepository.Ecf.Rid,
                EifLength = _dataRepository.Eif.ByteSize,
                EifRid = _dataRepository.Eif.Rid,
                EnfLength = _dataRepository.Enf.ByteSize,
                EnfRid = _dataRepository.Enf.Rid,
                Equipment = equipmentResult.AsEquipmentWelcome(),
                EsfLength = _dataRepository.Esf.ByteSize,
                EsfRid = _dataRepository.Esf.Rid,
                Experience = character.Exp,
                GuildName = "DansArmy",
                GuildRank = 0,
                GuildRankName = "The Boss",
                GuildTag = "DAN",
                MapFileSize = map.ByteSize,
                MapId = character.Map,
                MapRid = map.Rid,
                Name = character.Name,
                Stats = new CharacterStatsWelcome
                {
                    Base = new CharacterBaseStatsWelcome
                    {
                        Agi = character.Agi,
                        Cha = character.Cha,
                        Con = character.Con,
                        Str = character.Str,
                        Wis = character.Wis
                    },
                    Secondary = new CharacterSecondaryStats
                    {
                        Accuracy = 10,
                        Armor = 10,
                        Evade = 10,
                        MaxDamage = 150,
                        MinDamage = 100
                    },
                    Karma = character.Karma,
                    MaxSp = character.MaxSp,
                    MaxTp = character.MaxTp,
                    Tp = character.Tp,
                    MaxHp = character.MaxHp,
                    Hp = character.Hp,
                    SkillPoints = character.SkillPoints,
                    StatPoints = character.StatPoints
                },
                Title = character.Title ?? "",
                Usage = character.Usage,
                SessionId = playerState.SessionId,
                Level = character.Level,
                LoginMessageCode = character.Usage switch
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