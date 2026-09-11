using Acorn.Database.Repository;
using Acorn.Net.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresState(ClientState.EnteringGame)]
public class WelcomeAgreeClientPacketHandler(
    IDataFileRepository dataRepository,
    ILogger<WelcomeAgreeClientPacketHandler> logger)
    : IPacketHandler<WelcomeAgreeClientPacket>
{
    /// <summary>
    ///     Reads the pub-file part requested by the client. The SDK only exposes whole-file
    ///     serialization, so Acorn echoes the requested id but always sends the complete file;
    ///     multi-part pub splitting is not supported (matches reoserv's TODO).
    /// </summary>
    internal static int GetRequestedFileId(WelcomeAgreeClientPacket.IFileTypeData? fileTypeData) =>
        fileTypeData switch
        {
            WelcomeAgreeClientPacket.FileTypeDataEif data => data.FileId,
            WelcomeAgreeClientPacket.FileTypeDataEnf data => data.FileId,
            WelcomeAgreeClientPacket.FileTypeDataEsf data => data.FileId,
            WelcomeAgreeClientPacket.FileTypeDataEcf data => data.FileId,
            _ => 1
        };

    public async Task HandleAsync(PlayerState playerState,
        WelcomeAgreeClientPacket packet)
    {
        var fileId = GetRequestedFileId(packet.FileTypeData);
        var eoWriter = new EoWriter();

        if (packet.FileType == FileType.Emf)
        {
            var map = dataRepository.Maps.FirstOrDefault(map => map.Id == playerState.Character?.Map)?.Map;
            if (map is null)
            {
                logger.LogWarning("Could not find map {MapId} for character {Name} - disconnecting",
                    playerState.Character?.Map, playerState.Character?.Name);
                playerState.Disconnect();
                return;
            }

            map.Serialize(eoWriter);

            await playerState.Send(new InitInitServerPacket
            {
                ReplyCode = InitReply.FileEmf,
                ReplyCodeData = new InitInitServerPacket.ReplyCodeDataFileEmf
                {
                    MapFile = new MapFile
                    {
                        Content = eoWriter.ToByteArray()
                    }
                }
            });
            return;
        }

        Action serialise = packet.FileType switch
        {
            FileType.Eif => () => dataRepository.Eif.Serialize(eoWriter),
            FileType.Esf => () => dataRepository.Esf.Serialize(eoWriter),
            FileType.Enf => () => dataRepository.Enf.Serialize(eoWriter),
            FileType.Ecf => () => dataRepository.Ecf.Serialize(eoWriter),
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
                FileType.Ecf => InitReply.FileEcf,
                _ => throw new InvalidOperationException($"Unknown file type {packet.FileType}")
            },
            ReplyCodeData = packet.FileType switch
            {
                FileType.Enf => new InitInitServerPacket.ReplyCodeDataFileEnf
                {
                    PubFile = new PubFile
                    {
                        FileId = fileId,
                        Content = bytes
                    }
                },
                FileType.Ecf => new InitInitServerPacket.ReplyCodeDataFileEcf
                {
                    PubFile = new PubFile
                    {
                        FileId = fileId,
                        Content = bytes
                    }
                },
                FileType.Eif => new InitInitServerPacket.ReplyCodeDataFileEif
                {
                    PubFile = new PubFile
                    {
                        FileId = fileId,
                        Content = bytes
                    }
                },
                FileType.Esf => new InitInitServerPacket.ReplyCodeDataFileEsf
                {
                    PubFile = new PubFile
                    {
                        FileId = fileId,
                        Content = bytes
                    }
                },
                _ => throw new NotImplementedException($"{packet.FileType} is not supported")
            }
        });
    }

}
