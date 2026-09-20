using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player;
using Acorn.Shared.Caching;
using Acorn.Tests.Support;
using Acorn.Tests.TestSupport;
using Acorn.World.Map;
using Acorn.World.Npc;
using Acorn.World.Services.Arena;
using Acorn.World.Services.Map;
using Acorn.World.Services.Party;
using Acorn.World.Services.Player;
using Acorn.World.Services.Quest;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using System.Threading.Tasks;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.Net.PacketHandlers.Player;

/// <summary>
///     Damage is reported to the client in full, even when it overkills the target
///     (e.g. 105 damage against a 60 HP NPC shows 105), while HP is still clamped to 0.
/// </summary>
public class AttackUseClientPacketHandlerTests
{
    private const int OverkillDamage = 105;
    private const int NpcMaxHp = 60;

    private static AttackUseClientPacketHandler CreateHandler(
        IFormulaService formulaService,
        IDataFileRepository dataFiles,
        IMapTileService tileService,
        ILootService lootService)
    {
        var options = TestFactories.CreateServerOptions();
        options.AttackCooldownMs = 0;
        options.RangedDistance = 5;
        options.DropProtectionTicks = 0;

        return new AttackUseClientPacketHandler(
            () => DateTime.UtcNow,
            NullLogger<AttackUseClientPacketHandler>.Instance,
            formulaService,
            dataFiles,
            lootService,
            Microsoft.Extensions.Options.Options.Create(options),
            Substitute.For<ICharacterCacheService>(),
            Substitute.For<IPaperdollService>(),
            Substitute.For<IArenaService>(),
            Substitute.For<IPartyService>(),
            Substitute.For<IPlayerController>(),
            tileService,
            Substitute.For<IQuestService>(),
            Substitute.For<IMapItemService>(),
            Substitute.For<IMapController>(),
            new AcornMetrics());
    }

    private static NpcState CreateNpc(int index, int x, int y)
    {
        return new NpcState(new EnfRecord
        {
            Name = "TestNpc",
            Type = PubNpcType.Passive,
            Hp = NpcMaxHp,
            Experience = 0
        })
        {
            Index = index,
            Id = 1,
            X = x,
            Y = y,
            Hp = NpcMaxHp
        };
    }

    [Test]
    public async Task HandleAsync_WhenMeleeOverkillsNpc_ReportsFullDamageAndClampsHp()
    {
        // Arrange
        var formulaService = Substitute.For<IFormulaService>();
        formulaService.CalculateDamageToNpc(
                Arg.Any<Character>(), Arg.Any<EnfRecord>(), Arg.Any<int>(),
                Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>())
            .Returns(OverkillDamage);

        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Eif.Returns(new Eif());

        var tileService = Substitute.For<IMapTileService>();
        tileService.IsTileWalkable(Arg.Any<Emf>(), Arg.Any<Coords>()).Returns(true);
        tileService.InClientRange(Arg.Any<Coords>(), Arg.Any<Coords>()).Returns(true);

        var map = FakeMap.Create();
        var (player, communicator) = FakePlayer.Create("Tester", sessionId: 1);
        player.Character!.X = 5;
        player.Character.Y = 5;
        player.Character.Direction = Direction.Right;
        player.CurrentMap = map;
        map.Players[player.SessionId] = player;

        var npc = CreateNpc(index: 7, x: 6, y: 5);
        map.Npcs[npc.Index] = npc;

        var handler = CreateHandler(formulaService, dataFiles, tileService, Substitute.For<ILootService>());

        // Act
        await handler.HandleAsync(player, new AttackUseClientPacket { Direction = Direction.Right });

        // Assert
        npc.Hp.Should().Be(0, "HP is clamped at zero");

        var packets = DecodeSentPackets(communicator, player);
        var reply = packets.OfType<NpcReplyServerPacket>().Single();
        reply.Damage.Should().Be(OverkillDamage, "the full rolled damage is reported, not the 60 needed to kill");

        var killed = packets.OfType<NpcSpecServerPacket>().Single();
        killed.NpcKilledData.Damage.Should().Be(OverkillDamage);
    }

    private static List<IPacket> DecodeSentPackets(CapturingCommunicator communicator, PlayerState player)
    {
        var packets = new List<IPacket>();

        foreach (var frame in communicator.Sent)
        {
            // frame = [2-byte encoded length][action][family][encrypted payload]
            var body = frame.Skip(2).ToArray();
            var decrypted = DataEncrypter.SwapMultiples(
                DataEncrypter.Deinterleave(DataEncrypter.FlipMSB(body)),
                player.ServerEncryptionMulti);

            var reader = new EoReader(decrypted);
            var action = (PacketAction)reader.GetByte();
            var family = (PacketFamily)reader.GetByte();

            var resolver = new PacketResolver("Moffat.EndlessOnline.SDK.Protocol.Net.Server");
            var packet = resolver.Create(family, action);
            packet.Deserialize(reader.Slice());
            packets.Add(packet);
        }

        return packets;
    }
}
