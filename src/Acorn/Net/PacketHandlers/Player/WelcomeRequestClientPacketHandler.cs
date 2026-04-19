using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.World.Services.Quest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQuestService _questService;

    public WelcomeRequestClientPacketHandler(
        IDataFileRepository dataRepository,
        IStatCalculator statCalculator,
        IPaperdollService paperdollService,
        IServiceScopeFactory scopeFactory,
        IQuestService questService,
        ILogger<WelcomeRequestClientPacketHandler> logger
    )
    {
        _dataRepository = dataRepository;
        _statCalculator = statCalculator;
        _paperdollService = paperdollService;
        _scopeFactory = scopeFactory;
        _questService = questService;
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

        playerState.Character = CharacterMapper.FromDatabaseModel(character);

        // Load guild membership data
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();
            var guildMember = await db.GuildMembers
                .Include(m => m.Guild)
                .FirstOrDefaultAsync(m => m.CharacterName == playerState.Character.Name);

            if (guildMember?.Guild is not null)
            {
                playerState.Character.GuildTag = guildMember.GuildTag;
                playerState.Character.GuildName = guildMember.Guild.Name;
                playerState.Character.GuildRankIndex = guildMember.RankIndex;
                var ranks = guildMember.Guild.Ranks.Split(',');
                playerState.Character.GuildRankName =
                    guildMember.RankIndex >= 0 && guildMember.RankIndex < ranks.Length
                        ? ranks[guildMember.RankIndex]
                        : "";
            }
        }

        // Load quest progress
        await _questService.LoadQuestProgress(playerState.Character.Name!, playerState.Character);

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
                GuildName = playerState.Character.GuildName ?? "",
                GuildRank = playerState.Character.GuildRankIndex,
                GuildRankName = playerState.Character.GuildRankName ?? "",
                GuildTag = playerState.Character.GuildTag ?? "   ",
                MapFileSize = map.ByteSize,
                MapId = playerState.Character.Map,
                MapRid = map.Rid,
                Name = playerState.Character.Name,
                Stats = new CharacterStatsWelcome
                {
                    Base = new CharacterBaseStatsWelcome
                    {
                        Agi = playerState.Character.AdjAgi,
                        Cha = playerState.Character.AdjCha,
                        Con = playerState.Character.AdjCon,
                        Str = playerState.Character.AdjStr,
                        Wis = playerState.Character.AdjWis,
                        Intl = playerState.Character.AdjInt
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