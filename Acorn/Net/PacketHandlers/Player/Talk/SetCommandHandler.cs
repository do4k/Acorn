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

    public async Task HandleAsync(ConnectionHandler connectionHandler, string command, params string[] args)
    {
        if (args.Length < 3)
        {
            await connectionHandler.ServerMessage(Usage);
            return;
        }

        var target = _world.Players.FirstOrDefault(x =>
            string.Equals(x.Value.CharacterController.Data?.Name, args[0], StringComparison.CurrentCultureIgnoreCase)).Value;
        if (target is null)
        {
            await connectionHandler.ServerMessage($"ConnectionHandler {args[0]} not found.");
            return;
        }

        if (target.CharacterController.Data is null)
        {
            _logger.LogError("Tried set command on a character that has not been initialised");
            return;
        }

        if (!int.TryParse(args[2], out var value))
        {
            await connectionHandler.ServerMessage($"Value must be an integer. {Usage}");
            return;
        }

        Action adjustment = args[1].ToLower() switch
        {
            "admin" => () => target.CharacterController.Data.Admin = (AdminLevel)value,
            "class" => () => target.CharacterController.Data.Class = value,
            "gender" => () => target.CharacterController.Data.Gender = (Gender)value,
            "level" => () => target.CharacterController.Data.Level = value,
            "skin" => () => target.CharacterController.Data.Race = value,
            "exp" => () => target.CharacterController.Data.Exp = value,
            "maxhp" => () => target.CharacterController.Data.MaxHp = value,
            "hp" => () => target.CharacterController.Data.Hp = value,
            "maxtp" => () => target.CharacterController.Data.MaxTp = value,
            "tp" => () => target.CharacterController.Data.Tp = value,
            "maxsp" => () => target.CharacterController.Data.MaxSp = value,
            "sp" => () => target.CharacterController.Data.Sp = value,
            "str" => () => target.CharacterController.Data.Str = value,
            "wis" => () => target.CharacterController.Data.Wis = value,
            "agi" => () => target.CharacterController.Data.Agi = value,
            "con" => () => target.CharacterController.Data.Con = value,
            "cha" => () => target.CharacterController.Data.Cha = value,
            "statpoints" => () => target.CharacterController.Data.StatPoints = value,
            "skillpoints" => () => target.CharacterController.Data.SkillPoints = value,
            "karma" => () => target.CharacterController.Data.Karma = value,
            "sitstate" => () => target.CharacterController.Data.SitState = (SitState)Enum.Parse(typeof(SitState), args[3], true),
            "hidden" => () => target.CharacterController.Data.Hidden = bool.Parse(args[3]),
            "nointeract" => () => target.CharacterController.Data.NoInteract = bool.Parse(args[3]),
            "bankmax" => () => target.CharacterController.Data.BankMax = value,
            "goldbank" => () => target.CharacterController.Data.GoldBank = value,
            "usage" => () => target.CharacterController.Data.Usage = value,
            _ => async () => await connectionHandler.ServerMessage($"Attribute {args[2]} is not supported.")
        };

        adjustment();
        await connectionHandler.ServerMessage($"ConnectionHandler {args[0]} had {args[1]} updated to {value}.");
        await connectionHandler.Refresh();
    }
}