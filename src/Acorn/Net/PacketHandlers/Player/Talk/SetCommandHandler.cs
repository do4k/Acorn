using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net.Services;
using Acorn.Shared.Caching;
using Acorn.World;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class SetCommandHandler : ITalkHandler
{
    private const string UsageText = "Usage: $set <player> <attribute> <value>";
    private readonly ICharacterCacheService _characterCache;
    private readonly ILogger<SetCommandHandler> _logger;
    private readonly INotificationService _notifications;
    private readonly IPaperdollService _paperdollService;
    private readonly IPlayerController _playerController;
    private readonly IWorldQueries _world;

    public SetCommandHandler(IWorldQueries world, ILogger<SetCommandHandler> logger, INotificationService notifications,
        IPlayerController playerController, ICharacterCacheService characterCache,
        IPaperdollService paperdollService)
    {
        _world = world;
        _logger = logger;
        _notifications = notifications;
        _playerController = playerController;
        _characterCache = characterCache;
        _paperdollService = paperdollService;
    }

    public IReadOnlyList<string> Commands => ["set"];

    public string Usage => "<player> <attribute> <value>";

    // $set can change arbitrary character state (including admin level, see below),
    // so it is restricted to GameMaster and above.
    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 3)
        {
            await _notifications.SystemMessage(playerState, UsageText);
            return;
        }

        var target = _world.GetAllPlayers().FirstOrDefault(x =>
            string.Equals(x.Character?.Name, args[0], StringComparison.CurrentCultureIgnoreCase));
        if (target is null)
        {
            await _notifications.SystemMessage(playerState, $"Player {args[0]} not found.");
            return;
        }

        if (target.Character is null)
        {
            _logger.LogError("Tried set command on a character that has not been initialised");
            return;
        }

        // Changing an admin level is a privilege escalation vector; only the
        // highest admin tier may do it.
        if (args[1].Equals("admin", StringComparison.OrdinalIgnoreCase)
            && playerState.Character is { } caller
            && caller.Admin < AdminLevel.HighGameMaster)
        {
            await _notifications.SystemMessage(playerState, "Only a HighGameMaster can change admin levels.");
            return;
        }

        int Value() => int.TryParse(args[2], out var parsed)
            ? parsed
            : throw new ArgumentException($"Value for '{args[1]}' must be an integer. {UsageText}");

        Action adjustment = args[1].ToLower() switch
        {
            "admin" => () => target.Character.Admin = (AdminLevel)Value(),
            "class" => () => target.Character.Class = Value(),
            "gender" => () => target.Character.Gender = (Gender)Value(),
            "level" => () => target.Character.Level = Value(),
            "skin" => () => target.Character.Race = Value(),
            "exp" => () => target.Character.Exp = Value(),
            "maxhp" => () => target.Character.MaxHp = Value(),
            "hp" => () => target.Character.Hp = Value(),
            "maxtp" => () => target.Character.MaxTp = Value(),
            "tp" => () => target.Character.Tp = Value(),
            "maxsp" => () => target.Character.MaxSp = Value(),
            "sp" => () => target.Character.Sp = Value(),
            "str" => () => target.Character.Str = Value(),
            "wis" => () => target.Character.Wis = Value(),
            "int" => () => target.Character.Int = Value(),
            "agi" => () => target.Character.Agi = Value(),
            "con" => () => target.Character.Con = Value(),
            "cha" => () => target.Character.Cha = Value(),
            "armor" => () => target.Character.Paperdoll.Armor = Value(),
            "hat" => () => target.Character.Paperdoll.Hat = Value(),
            "shield" => () => target.Character.Paperdoll.Shield = Value(),
            "weapon" => () => target.Character.Paperdoll.Weapon = Value(),
            "gloves" => () => target.Character.Paperdoll.Gloves = Value(),
            "boots" => () => target.Character.Paperdoll.Boots = Value(),
            "statpoints" => () => target.Character.StatPoints = Value(),
            "skillpoints" => () => target.Character.SkillPoints = Value(),
            "karma" => () => target.Character.Karma = Value(),
            "sitstate" => () => target.Character.SitState = Enum.Parse<SitState>(args[2], true),
            "hidden" => () => target.Character.Hidden = ParseBool(args[2]),
            "nointeract" => () => target.Character.NoInteract = ParseBool(args[2]),
            "bankmax" => () => target.Character.BankMax = Value(),
            "goldbank" => () => target.Character.GoldBank = Value(),
            "usage" => () => target.Character.Usage = Value(),
            "haircolor" => () => target.Character.HairColor = Value(),
            "hairstyle" => () => target.Character.HairStyle = Value(),
            _ => () => throw new ArgumentException($"{args[1]} is not a recognised attribute. {UsageText}")
        };

        try
        {
            adjustment();
            await _notifications.SystemMessage(playerState, $"Player {args[0]} had {args[1]} updated to {args[2]}.");
            await _playerController.RefreshAsync(playerState);

            // Cache the target player's state after adjustment
            await target.CacheCharacterStateAsync(_characterCache, _paperdollService);

            // Send stat update to target player if they're online
            await SendStatUpdateToPlayer(target);
        }
        catch (Exception ex)
        {
            var message = ex is ArgumentException
                ? ex.Message
                : $"{args[1]} is not a recognised attribute. {UsageText}";
            await _notifications.SystemMessage(playerState, message);
            _logger.LogError(ex, "Failed to set attribute {Attribute} for player {Player}", args[1], args[0]);
        }
    }

    private static bool ParseBool(string value)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return value switch
        {
            "1" => true,
            "0" => false,
            _ => throw new ArgumentException($"'{value}' is not a valid true/false value.")
        };
    }

    private async Task SendStatUpdateToPlayer(PlayerState target)
    {
        if (target.Character is null)
            return;

        var character = target.Character;
        await target.Send(new StatSkillPlayerServerPacket
        {
            StatPoints = character.StatPoints,
            Stats = new CharacterStatsUpdate
            {
                MaxHp = character.MaxHp,
                MaxTp = character.MaxTp,
                MaxSp = character.MaxSp,
                BaseStats = new CharacterBaseStats
                {
                    Str = character.AdjStr,
                    Intl = character.AdjInt,
                    Wis = character.AdjWis,
                    Agi = character.AdjAgi,
                    Con = character.AdjCon,
                    Cha = character.AdjCha
                },
                SecondaryStats = new CharacterSecondaryStats
                {
                    MinDamage = character.MinDamage,
                    MaxDamage = character.MaxDamage,
                    Accuracy = character.Accuracy,
                    Evade = character.Evade,
                    Armor = character.Armor
                }
            }
        });
    }
}
