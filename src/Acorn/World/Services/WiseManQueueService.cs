using System.Threading.Channels;
using Acorn.Infrastructure.Gemini;
using Acorn.Net;
using Acorn.Options;
using Acorn.World.Npc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services;

/// <summary>
///     Request to get a response from the Wise Man.
/// </summary>
public record WiseManRequest(PlayerState Player, string Query, NpcState WiseManNpc);

/// <summary>
///     Hosted service that processes Wise Man requests from a queue.
/// </summary>
public class WiseManQueueService : BackgroundService
{
    private readonly Channel<WiseManRequest> _channel;
    private readonly ILogger<WiseManQueueService> _logger;
    private readonly IWiseManAgent _wiseManAgent;
    private readonly WiseManAgentOptions _wiseManOptions;

    public WiseManQueueService(
        IWiseManAgent wiseManAgent,
        ILogger<WiseManQueueService> logger,
        IOptions<WiseManAgentOptions> wiseManOptions)
    {
        _wiseManAgent = wiseManAgent;
        _logger = logger;
        _wiseManOptions = wiseManOptions.Value;
        _channel = Channel.CreateBounded<WiseManRequest>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _logger.LogInformation("WiseManQueueService constructed");
    }

    /// <summary>
    ///     Queue a request for the Wise Man to respond to.
    /// </summary>
    public bool TryEnqueue(WiseManRequest request)
    {
        // Only enqueue if player is in range of a Wise Man NPC
        var map = request.Player.CurrentMap;
        if (map == null)
        {
            return false;
        }

        // Find Wise Man NPC on the map
        var wiseManNpc =
            map.Npcs.FirstOrDefault(n => n.Data.Name.Contains("Wise Man", StringComparison.OrdinalIgnoreCase));
        if (wiseManNpc == null)
        {
            return false;
        }

        // Create a new WiseManRequest with the NPC reference
        var queuedRequest = request with { WiseManNpc = wiseManNpc };
        var success = _channel.Writer.TryWrite(queuedRequest);
        if (success)
        {
            _logger.LogInformation("Queued Wise Man request from {Player}: {Query}",
                request.Player.Character?.Name ?? "Unknown", request.Query);
        }
        else
        {
            _logger.LogWarning("Failed to queue Wise Man request - channel full");
        }

        return success;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_wiseManOptions.Enabled)
        {
            _logger.LogInformation("Wise Man agent is disabled. WiseManQueueService will shut down.");
            return;
        }

        _logger.LogInformation("WiseManQueueService ExecuteAsync started");
        _logger.LogInformation("Wise Man queue service started and listening for requests");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for data to be available
                if (!await _channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    continue;
                }

                // Process available data
                while (_channel.Reader.TryRead(out var request))
                {
                    try
                    {
                        _logger.LogDebug("Processing Wise Man request from {Player}",
                            request.Player.Character?.Name ?? "Unknown");

                        await ProcessRequestAsync(request);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Wise Man request");
                    }

                    // Small delay to avoid rate limiting
                    await Task.Delay(500, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Wise Man queue service stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wise Man queue service encountered an error");
            }
        }
    }

    private async Task ProcessRequestAsync(WiseManRequest? request)
    {
        if (request == null || request.Player.Character == null || request.Player.CurrentMap == null ||
            request.WiseManNpc == null)
        {
            return;
        }

        var playerName = request.Player.Character.Name ?? "Adventurer";
        var response = await _wiseManAgent.GetWiseManResponseAsync(playerName, request.Query);

        if (string.IsNullOrEmpty(response))
        {
            response = "The ancient wisdom escapes me at this moment, young one...";
        }

        // Send the response as an NPC message to nearby players
        await SendWiseManMessageAsync(request.Player, response, request.WiseManNpc);
    }

    private async Task SendWiseManMessageAsync(PlayerState player, string message, NpcState wiseManNpc)
    {
        if (player.CurrentMap == null)
        {
            return;
        }

        // Split Gemini response into up to 3 parts
        var parts = message.Split("Response part ",
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Trim(':', ' ', '1', '2', '3')).ToList();
        if (parts.Count == 0)
        {
            parts.Add(message); // fallback if not formatted
        }

        foreach (var part in parts)
        {
            // Clamp the message part to the max response length
            var clampedPart = part;
            if (clampedPart.Length > _wiseManOptions.MaxResponseLength)
            {
                clampedPart = clampedPart.Substring(0, _wiseManOptions.MaxResponseLength) + "...";
            }

            // Broadcast to chat log/history as if "Wise Man" is a player
            var chatLogPacket = new TalkMsgServerPacket
            {
                PlayerName = "Wise Man",
                Message = clampedPart
            };
            foreach (var mapPlayer in player.CurrentMap.Players)
            {
                try
                {
                    await mapPlayer.Send(chatLogPacket);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send Wise Man chat log message to player");
                }
            }

            var npcIndex = player.CurrentMap.Npcs.ToList().IndexOf(wiseManNpc);
            var chatUpdate = new NpcUpdateChat
            {
                NpcIndex = npcIndex,
                Message = clampedPart
            };
            var npcPacket = new NpcPlayerServerPacket
            {
                Chats = [chatUpdate]
            };
            foreach (var mapPlayer in player.CurrentMap.Players)
            {
                try
                {
                    await mapPlayer.Send(npcPacket);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send Wise Man NPC message to player");
                }
            }

            await Task.Delay(3000);
        }
    }
}