using Acorn.Extensions;
using Acorn.Net;
using Acorn.Net.Models;
using Acorn.Net.PacketHandlers;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

internal class WelcomeMsgClientPacketHandler : IPacketHandler<WelcomeMsgClientPacket>
{
    private readonly string[] _newsTxt;
    private readonly IWorldQueries _world;

    public WelcomeMsgClientPacketHandler(
        IWorldQueries worldState
    )
    {
        _newsTxt = File.ReadAllLines("Data/news.txt");
        _world = worldState;
    }

    public async Task HandleAsync(
        PlayerState playerState,
        WelcomeMsgClientPacket packet)
    {
        playerState.ClientState = ClientState.InGame;
        var map = _world.FindMap(playerState.Character?.Map ?? -1);
        if (map is null)
        {
            return;
        }

        await map.NotifyEnter(playerState);

        await playerState.Send(new WelcomeReplyServerPacket
        {
            WelcomeCode = WelcomeCode.EnterGame,
            WelcomeCodeData = new WelcomeReplyServerPacket.WelcomeCodeDataEnterGame
            {
                Items = playerState.Character?.Items().ToList(),
                News = new List<string> { " " }
                    .Concat(_newsTxt.Concat(Enumerable.Range(0, 8 - _newsTxt.Length).Select(_ => ""))).ToList(),
                Weight = new Weight
                {
                    Current = 0,
                    Max = 100
                },
                Nearby = map.AsNearbyInfo()
            }
        });

        // Send bots as a separate packet after a small delay to ensure client is ready
        // This fixes the issue where bots are invisible on initial login
        if (map.ArenaBots.Count > 0)
        {
            await Task.Delay(150); // Give client time to process welcome packet
            
            var botCharacters = map.ArenaBots
                .Select(bot => bot.AsCharacterMapInfo())
                .ToList();
            
            await playerState.Send(new PlayersAgreeServerPacket
            {
                Nearby = new NearbyInfo
                {
                    Characters = botCharacters,
                    Npcs = new List<NpcMapInfo>(),
                    Items = new List<ItemMapInfo>()
                }
            });
        }
    }

}