using System.Collections.Concurrent;
using Acorn.Game.Models;
using Acorn.Options;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Acorn.Tests.TestHelpers;

internal static class GameTestFactory
{
    public static IOptions<ServerOptions> ServerOptions(
        int maxStat = 10000,
        int maxSkillLevel = 100,
        int statPerLevel = 3,
        int skillPerLevel = 4)
    {
        return OptionsFactory.Create(new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 0, Y = 0, Map = 0 },
            Hosting = new HostingOptions
            {
                SLN = new SLNOptions
                {
                    Enabled = false,
                    Url = "http://localhost",
                    PingRate = 5,
                    UserAgent = "test",
                    Zone = "test",
                    ServerName = "test",
                    Site = "http://localhost"
                },
                HostName = "localhost",
                Port = 0,
                WebSocketPort = 0
            },
            TickRate = 1000,
            MaxStat = maxStat,
            MaxSkillLevel = maxSkillLevel,
            StatPerLevel = statPerLevel,
            SkillPerLevel = skillPerLevel
        });
    }

    public static Ecf Ecf()
    {
        return new Ecf
        {
            Classes = new List<EcfRecord>
            {
                new() { Name = "Peasant", StatGroup = 4 }
            }
        };
    }

    public static Character Character(int level = 0, int statPoints = 0, int skillPoints = 0)
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            Class = 1,
            Level = level,
            Hp = 10,
            MaxHp = 10,
            Tp = 10,
            MaxTp = 10,
            Sp = 20,
            MaxSp = 20,
            StatPoints = statPoints,
            SkillPoints = skillPoints,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells(new ConcurrentBag<Spell>())
        };
    }
}
