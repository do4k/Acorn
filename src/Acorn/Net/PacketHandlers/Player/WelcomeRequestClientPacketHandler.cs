using Acorn.Database;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Net.Models;
using Acorn.Options;
using Acorn.World.Services.Quest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly ServerOptions _serverOptions;

    public WelcomeRequestClientPacketHandler(
        IDataFileRepository dataRepository,
        IStatCalculator statCalculator,
        IPaperdollService paperdollService,
        IServiceScopeFactory scopeFactory,
        IQuestService questService,
        IOptions<ServerOptions> serverOptions,
        ILogger<WelcomeRequestClientPacketHandler> logger
    )
    {
        _dataRepository = dataRepository;
        _statCalculator = statCalculator;
        _paperdollService = paperdollService;
        _scopeFactory = scopeFactory;
        _questService = questService;
        _serverOptions = serverOptions.Value;
        _logger = logger;
    }

    /// <summary>
    ///     Reports a higher admin level to the client than the character's stored level to
    ///     unlock the client-side admin UI, matching eoserv. Acorn stores a single effective
    ///     admin level per character (there is no separate "source access"), so any character
    ///     with admin commands (Guardian and above) is reported as HighGameMaster.
    /// </summary>
    internal static AdminLevel GetClientAdminLevel(AdminLevel admin) =>
        admin >= AdminLevel.Guardian ? AdminLevel.HighGameMaster : admin;

    public async Task HandleAsync(PlayerState playerState,
        WelcomeRequestClientPacket packet)
    {
        if (playerState.Character is not null)
        {
            _logger.LogWarning("Player {SessionId} attempted to select a character twice", playerState.SessionId);
            return;
        }

        var characters = playerState.Account?.Characters;
        if (characters is null || packet.CharacterId < 0 || packet.CharacterId >= characters.Count)
        {
            _logger.LogError("Could not find character at index {CharacterId}", packet.CharacterId);
            return;
        }

        var character = characters[packet.CharacterId];

        //playerConnection.SessionId = _sessionGenerator.Generate();
        var map = _dataRepository.Maps.FirstOrDefault(map => map.Id == character.Map)?.Map;
        if (map is null)
        {
            // Match reoserv/eoserv: fall back to the rescue/spawn map, otherwise disconnect.
            var rescueMapId = _serverOptions.Rescue?.Map ?? _serverOptions.NewCharacter.Map;
            var rescueMap = _dataRepository.Maps.FirstOrDefault(m => m.Id == rescueMapId)?.Map;
            if (rescueMap is null)
            {
                _logger.LogError(
                    "Could not find map {MapId} or rescue map {RescueMapId} for character {Name} - disconnecting",
                    character.Map, rescueMapId, character.Name);
                playerState.Disconnect();
                return;
            }

            _logger.LogWarning(
                "Could not find map {MapId} for character {Name} - using rescue map {RescueMapId}",
                character.Map, character.Name, rescueMapId);
            character.Map = rescueMapId;
            character.X = _serverOptions.Rescue?.X ?? _serverOptions.NewCharacter.X;
            character.Y = _serverOptions.Rescue?.Y ?? _serverOptions.NewCharacter.Y;
            map = rescueMap;
        }

        playerState.Character = CharacterMapper.FromDatabaseModel(character);
        playerState.ClientState = ClientState.EnteringGame;

        // Guild leaders are reported as rank 1 to unlock the client guild-management tools (eoserv behaviour).
        var clientGuildRank = playerState.Character.GuildRankIndex;

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

                clientGuildRank = guildMember.RankIndex <= 1 ? 1 : guildMember.RankIndex;
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
                Admin = GetClientAdminLevel(playerState.Character.Admin),
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
                GuildRank = clientGuildRank,
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
                    JailMap = _serverOptions.Jail?.Map ?? _serverOptions.Rescue?.Map ?? _serverOptions.NewCharacter.Map,
                    RescueMap = _serverOptions.Rescue?.Map ?? _serverOptions.NewCharacter.Map,
                    RescueCoords = new Coords
                    {
                        X = _serverOptions.Rescue?.X ?? _serverOptions.NewCharacter.X,
                        Y = _serverOptions.Rescue?.Y ?? _serverOptions.NewCharacter.Y
                    },
                    SpyAndLightGuideFloodRate = _serverOptions.FloodRates.SpyAndLightGuide,
                    GuardianFloodRate = _serverOptions.FloodRates.Guardian,
                    GameMasterFloodRate = _serverOptions.FloodRates.GameMaster,
                    HighGameMasterFloodRate = _serverOptions.FloodRates.HighGameMaster
                }
            }
        });
    }

}
