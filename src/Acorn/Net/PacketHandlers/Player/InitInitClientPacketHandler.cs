using Acorn.Net.Models;
using Acorn.Options;
using Acorn.World;
using Acorn.World.Services.Bans;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class InitInitClientPacketHandler(
    ILogger<InitInitClientPacketHandler> logger,
    IOptions<ServerOptions> serverOptions,
    IWorldQueries world,
    IBanService banService)
    : IPacketHandler<InitInitClientPacket>
{
    private readonly ILogger<InitInitClientPacketHandler> _logger = logger;
    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private readonly IWorldQueries _world = world;
    private readonly IBanService _banService = banService;

    public async Task HandleAsync(PlayerState playerState, InitInitClientPacket packet)
    {
        playerState.Hdid = packet.Hdid;

        if (!ValidateHdid(playerState, packet.Hdid))
        {
            return;
        }

        if (_banService.IsBanned(BanKeys.Hdid(packet.Hdid)))
        {
            _logger.LogWarning("Rejected banned HDID {Hdid} from {Origin}",
                packet.Hdid, playerState.Communicator.GetConnectionOrigin());
            await SendBannedAsync(playerState);
            return;
        }

        if (_serverOptions.CheckVersion)
        {
            if (_serverOptions.ProtocolVersion > 0 && playerState.ClientProtocolVersion != _serverOptions.ProtocolVersion)
            {
                _logger.LogWarning(
                    "Rejected client with unsupported protocol {Protocol} (expected {Expected}) from {Origin}",
                    playerState.ClientProtocolVersion, _serverOptions.ProtocolVersion,
                    playerState.Communicator.GetConnectionOrigin());
                await SendOutOfDateAsync(playerState);
                return;
            }

            if (!ClientVersionValidator.IsSupported(packet.Version, _serverOptions.MinVersion, _serverOptions.MaxVersion))
            {
                _logger.LogWarning(
                    "Rejected out-of-date client version {Major}.{Minor}.{Patch} (supported {Min}-{Max}) from {Origin}",
                    packet.Version.Major, packet.Version.Minor, packet.Version.Patch,
                    _serverOptions.MinVersion, _serverOptions.MaxVersion,
                    playerState.Communicator.GetConnectionOrigin());
                await SendOutOfDateAsync(playerState);
                return;
            }
        }

        playerState.Sequencer.SetStart(playerState.StartSequence.Value);
        playerState.ClientEncryptionMulti = playerState.Rnd.Next(7) + 6;
        playerState.ServerEncryptionMulti = playerState.Rnd.Next(7) + 6;

        _logger.LogDebug("Sending Init Server Packet with Seq 1: {Seq1}, Seq 2: {Seq2} PlayerId: {PlayerId}",
            playerState.StartSequence.Seq1, playerState.StartSequence.Seq2, playerState.SessionId);
        await playerState.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.Ok,
            ReplyCodeData = new InitInitServerPacket.ReplyCodeDataOk
            {
                Seq1 = playerState.StartSequence.Seq1,
                Seq2 = playerState.StartSequence.Seq2,
                ClientEncryptionMultiple = playerState.ClientEncryptionMulti,
                ServerEncryptionMultiple = playerState.ServerEncryptionMulti,
                PlayerId = playerState.SessionId,
                ChallengeResponse = ServerVerifier.Hash(packet.Challenge)
            }
        });

        playerState.ClientState = ClientState.Initialized;
    }

    private bool ValidateHdid(PlayerState playerState, string hdid)
    {
        if (_serverOptions.IgnoreHdid)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(hdid))
        {
            _logger.LogWarning("Rejected connection with missing HDID from {Origin}",
                playerState.Communicator.GetConnectionOrigin());
            playerState.Disconnect();
            return false;
        }

        var maxPerPc = _serverOptions.MaxConnectionsPerPC;
        if (maxPerPc <= 0)
        {
            return true;
        }

        var connectionsForHdid = _world.GetAllPlayers()
            .Count(p => p.SessionId != playerState.SessionId && string.Equals(p.Hdid, hdid, StringComparison.OrdinalIgnoreCase));

        if (connectionsForHdid >= maxPerPc)
        {
            _logger.LogWarning("Rejected connection: too many connections from HDID {Hdid} ({Count}/{Max})",
                hdid, connectionsForHdid, maxPerPc);
            playerState.Disconnect();
            return false;
        }

        return true;
    }

    private async Task SendOutOfDateAsync(PlayerState playerState)
    {
        var min = ParseVersion(_serverOptions.MinVersion);
        await playerState.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.OutOfDate,
            ReplyCodeData = new InitInitServerPacket.ReplyCodeDataOutOfDate
            {
                Version = new Moffat.EndlessOnline.SDK.Protocol.Net.Version
                {
                    Major = min.Major,
                    Minor = min.Minor,
                    Patch = min.Patch
                }
            }
        });
        playerState.Disconnect();
    }

    private static (int Major, int Minor, int Patch) ParseVersion(string value)
    {
        return System.Version.TryParse(value, out var parsed)
            ? (parsed.Major, parsed.Minor, parsed.Build)
            : (0, 0, 0);
    }

    private async Task SendBannedAsync(PlayerState playerState)
    {
        await playerState.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.Banned,
            ReplyCodeData = new InitInitServerPacket.ReplyCodeDataBanned
            {
                BanType = InitBanType.Permanent
            }
        });
        playerState.Disconnect();
    }
}
