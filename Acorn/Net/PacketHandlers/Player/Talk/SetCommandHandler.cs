using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class SetCommandHandler : ITalkHandler
{
    private const string Usage = "Usage: $set <player> <attribute> <value>";
    private readonly ILogger<SetCommandHandler> _logger;
    private readonly WorldState _world;

    public SetCommandHandler(WorldState world, ILogger<SetCommandHandler> logger)
    {
        _world = world;
        _logger = logger;
    }

    public bool CanHandle(string command)
    {
        return string.Equals("set", command, StringComparison.InvariantCultureIgnoreCase);
    }

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 3)
        {
            await playerState.ServerMessage(Usage);
            return;
        }

        var target = _world.Players.FirstOrDefault(x =>
            string.Equals(x.Value.Character?.Name, args[0], StringComparison.CurrentCultureIgnoreCase)).Value;
        if (target is null)
        {
            await playerState.ServerMessage($"Player {args[0]} not found.");
            return;
        }

        if (target.Character is null)
        {
            _logger.LogError("Tried set command on a character that has not been initialised");
            return;
        }

        if (!int.TryParse(args[2], out var value))
        {
            await playerState.ServerMessage($"Value must be an integer. {Usage}");
            return;
        }

        Action adjustment = args[1].ToLower() switch
        {
            "admin" => () => target.Character.Admin = (AdminLevel)value,
            "class" => () => target.Character.Class = value,
            "gender" => () => target.Character.Gender = (Gender)value,
            "level" => () => target.Character.Level = value,
            "skin" => () => target.Character.Race = value,
            "exp" => () => target.Character.Exp = value,
            "maxhp" => () => target.Character.MaxHp = value,
            "hp" => () => target.Character.Hp = value,
            "maxtp" => () => target.Character.MaxTp = value,
            "tp" => () => target.Character.Tp = value,
            "maxsp" => () => target.Character.MaxSp = value,
            "sp" => () => target.Character.Sp = value,
            "str" => () => target.Character.Str = value,
            "wis" => () => target.Character.Wis = value,
            "agi" => () => target.Character.Agi = value,
            "con" => () => target.Character.Con = value,
            "cha" => () => target.Character.Cha = value,
            "armor" => () => target.Character.Paperdoll.Armor = value,
            "hat" => () => target.Character.Paperdoll.Hat = value,
            "shield" => () => target.Character.Paperdoll.Shield = value,
            "weapon" => () => target.Character.Paperdoll.Weapon = value,
            "gloves" => () => target.Character.Paperdoll.Gloves = value,
            "boots" => () => target.Character.Paperdoll.Boots = value,
            "statpoints" => () => target.Character.StatPoints = value,
            "skillpoints" => () => target.Character.SkillPoints = value,
            "karma" => () => target.Character.Karma = value,
            "sitstate" => () => target.Character.SitState = (SitState)Enum.Parse(typeof(SitState), args[3], true),
            "hidden" => () => target.Character.Hidden = bool.Parse(args[3]),
            "nointeract" => () => target.Character.NoInteract = bool.Parse(args[3]),
            "bankmax" => () => target.Character.BankMax = value,
            "goldbank" => () => target.Character.GoldBank = value,
            "usage" => () => target.Character.Usage = value,
            _ => throw new ArgumentException($"Unknown attribute: {args[1]}. {Usage}")
        };
        
        try 
        {
            adjustment();
            await playerState.ServerMessage($"Player {args[0]} had {args[1]} updated to {value}.");
            await playerState.Refresh();
        }
        catch (Exception ex)
        {
            await playerState.ServerMessage($"{args[1]} is not a recognised attribute. {Usage}");
            _logger.LogError(ex, "Failed to set attribute {Attribute} for player {Player}", args[1], args[0]);
            return;
        }
    }
}