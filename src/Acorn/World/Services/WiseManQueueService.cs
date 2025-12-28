using System.Threading.Channels;
using Acorn.Infrastructure.Gemini;
using Acorn.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services;

/// <summary>
/// Request to get a response from the Wise Man.
/// </summary>
public record WiseManRequest(PlayerState Player, string Query);

/// <summary>
/// Hosted service that processes Wise Man requests from a queue.
/// </summary>
public class WiseManQueueService : IHostedService, IDisposable
{
    private readonly Channel<WiseManRequest> _channel;
    private readonly IWiseManAgent _wiseManAgent;
    private readonly ILogger<WiseManQueueService> _logger;
    private Task? _executingTask;
    private CancellationTokenSource? _stoppingCts;

    public WiseManQueueService(
        IWiseManAgent wiseManAgent,
        ILogger<WiseManQueueService> logger)
    {
        _wiseManAgent = wiseManAgent;
        _logger = logger;
        _channel = Channel.CreateBounded<WiseManRequest>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _logger.LogInformation("WiseManQueueService constructed");
    }

    /// <summary>
    /// Queue a request for the Wise Man to respond to.
    /// </summary>
    public bool TryEnqueue(WiseManRequest request)
    {
        var success = _channel.Writer.TryWrite(request);
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WiseManQueueService StartAsync called");
        
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Start the background processing task
        _executingTask = Task.Run(() => ExecuteAsync(_stoppingCts.Token), _stoppingCts.Token);
        
        _logger.LogInformation("WiseManQueueService background task started");
        
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WiseManQueueService StopAsync called");
        
        if (_executingTask == null)
            return;

        try
        {
            _stoppingCts?.Cancel();
        }
        finally
        {
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WiseManQueueService ExecuteAsync started");
        _logger.LogInformation("Wise Man queue service started and listening for requests");
        
        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Wise Man queue service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wise Man queue service encountered an error");
        }
    }

    private async Task ProcessRequestAsync(WiseManRequest request)
    {
        if (request.Player.Character == null || request.Player.CurrentMap == null)
            return;

        var playerName = request.Player.Character.Name ?? "Adventurer";
        var response = await _wiseManAgent.GetWiseManResponseAsync(playerName, request.Query);

        if (string.IsNullOrEmpty(response))
        {
            response = "The ancient wisdom escapes me at this moment, young one...";
        }

        // Send the response as an NPC message to nearby players
        await SendWiseManMessageAsync(request.Player, response);
    }

    private async Task SendWiseManMessageAsync(PlayerState player, string message)
    {
        if (player.CurrentMap == null)
            return;

        var packet = new TalkMsgServerPacket
        {
            PlayerName = "Wise Man",
            Message = message
        };

        // Broadcast to all players on the map
        foreach (var mapPlayer in player.CurrentMap.Players)
        {
            try
            {
                await mapPlayer.Send(packet);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send Wise Man message to player");
            }
        }
    }

    public void Dispose()
    {
        _stoppingCts?.Cancel();
        _stoppingCts?.Dispose();
    }
}

