using Acorn.Database.Repository;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class WelcomeAgreeClientPacketHandler(IDataFileRepository dataRepository)
    : IPacketHandler<WelcomeAgreeClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        WelcomeAgreeClientPacket packet)
    {
        var eoWriter = new EoWriter();
        Action serialise = packet.FileType switch
        {
            FileType.Eif => () => dataRepository.Eif.Serialize(eoWriter),
            FileType.Esf => () => dataRepository.Esf.Serialize(eoWriter),
            FileType.Enf => () => dataRepository.Enf.Serialize(eoWriter),
            FileType.Ecf => () => dataRepository.Ecf.Serialize(eoWriter),
            FileType.Emf => () =>
            {
                var map = dataRepository.Maps.FirstOrDefault(map => map.Id == playerState.Character?.Map)?.Map ??
                          throw new ArgumentOutOfRangeException(
                              $"Could not find map {playerState.Character?.Map} for character {playerState.Character?.Name}");

                map.Serialize(eoWriter);
            },
            _ => throw new InvalidOperationException($"Unknown file type {packet.FileType}")
        };
        serialise();

        var bytes = eoWriter.ToByteArray();

        await playerState.Send(new InitInitServerPacket
        {
            ReplyCode = packet.FileType switch
            {
                FileType.Eif => InitReply.FileEif,
                FileType.Esf => InitReply.FileEsf,
                FileType.Enf => InitReply.FileEnf,
                FileType.Emf => InitReply.FileEmf,
                FileType.Ecf => InitReply.FileEcf,
                _ => throw new InvalidOperationException($"Unknown file type {packet.FileType}")
            },
            ReplyCodeData = packet.FileType switch
            {
                FileType.Enf => new InitInitServerPacket.ReplyCodeDataFileEnf
                {
                    PubFile = new PubFile
                    {
                        FileId = 1,
                        Content = bytes
                    }
                },
                FileType.Emf => new InitInitServerPacket.ReplyCodeDataFileEmf
                {
                    MapFile = new MapFile
                    {
                        Content = bytes
                    }
                },
                FileType.Ecf => new InitInitServerPacket.ReplyCodeDataFileEcf
                {
                    PubFile = new PubFile
                    {
                        FileId = 1,
                        Content = bytes
                    }
                },
                FileType.Eif => new InitInitServerPacket.ReplyCodeDataFileEif
                {
                    PubFile = new PubFile
                    {
                        FileId = 1,
                        Content = bytes
                    }
                },
                FileType.Esf => new InitInitServerPacket.ReplyCodeDataFileEsf
                {
                    PubFile = new PubFile
                    {
                        FileId = 1,
                        Content = bytes
                    }
                },
                _ => throw new NotImplementedException($"{packet.FileType} is not supported")
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (WelcomeAgreeClientPacket)packet);
    }
}