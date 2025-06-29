using Acorn.Controllers;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class WelcomeRequestClientPacketHandler : IPacketHandler<WelcomeRequestClientPacket>
{
    private readonly IDataFileRepository _dataRepository;
    private readonly ILogger<WelcomeRequestClientPacketHandler> _logger;
    private readonly ICharacterControllerFactory _characterControllerFactory;

    public WelcomeRequestClientPacketHandler(
        IDataFileRepository dataRepository,
        ICharacterControllerFactory characterControllerFactory,
        ILogger<WelcomeRequestClientPacketHandler> logger
    )
    {
        _characterControllerFactory = characterControllerFactory;
        _dataRepository = dataRepository;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectionHandler connectionHandler,
        WelcomeRequestClientPacket packet)
    {
        if (connectionHandler.Account is null)
        {
            _logger.LogError("Account has not been initialised for player {PlayerId}", connectionHandler.SessionId);
            return;
        }
        var character = connectionHandler.Account.Characters[packet.CharacterId];

        //playerConnection.SessionId = _sessionGenerator.Generate();
        var map = _dataRepository.Maps.FirstOrDefault(map => map.Id == character.Map)?.Map;
        if (map is null)
        {
            _logger.LogError("Could not find map {MapId} for character {Name}", character.Map, character.Name);
            return;
        }
        connectionHandler.CharacterController = _characterControllerFactory.Create(character);
        connectionHandler.CharacterController.SetStats(_dataRepository.Ecf);

        var equipmentResult = connectionHandler.CharacterController.GetEquipment();

        await connectionHandler.Send(new WelcomeReplyServerPacket
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
                SessionId = connectionHandler.SessionId,
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

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (WelcomeRequestClientPacket)packet);
    }
}