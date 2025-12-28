using Acorn.Infrastructure.Gemini;
using Acorn.World.Services;
using Microsoft.Extensions.Logging;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
/// Handler for messages directed at the Wise Man NPC.
/// Triggers AI-generated responses when players say "Hey Wise Man {query}".
/// </summary>
public class WiseManTalkHandler
{
    private readonly WiseManQueueService _queueService;
    private readonly ILogger<WiseManTalkHandler> _logger;

    private static readonly string[] TriggerPhrases =
    [
        "hey wise man",
        "hi wise man",
        "hello wise man",
        "wise man",
        "dear wise man",
        "oh wise man"
    ];

    public WiseManTalkHandler(
        WiseManQueueService queueService,
        ILogger<WiseManTalkHandler> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    /// <summary>
    /// Check if a message is directed at the Wise Man and queue a response if so.
    /// </summary>
    /// <returns>True if the message was handled (directed at Wise Man)</returns>
    public bool TryHandleMessage(PlayerState playerState, string message)
    {
        if (playerState.Character == null || playerState.CurrentMap == null)
            return false;

        var lowerMessage = message.ToLowerInvariant().Trim();

        foreach (var trigger in TriggerPhrases)
        {
            if (!lowerMessage.StartsWith(trigger))
            {
                continue;
            }

            // Extract the query after the trigger phrase
            var query = message[trigger.Length..].Trim();
            // Remove leading punctuation like comma or colon
            if (query.StartsWith(',') || query.StartsWith(':') || query.StartsWith('!'))
            {
                query = query[1..].Trim();
            }
            if (string.IsNullOrWhiteSpace(query))
            {
                query = "greet me";
            }

            // Find Wise Man NPC on the map
            var map = playerState.CurrentMap;
            var wiseManNpc = map.Npcs.FirstOrDefault(n => n.Data.Name.Contains("Wise Man", StringComparison.OrdinalIgnoreCase));
            if (wiseManNpc == null)
            {
                _logger.LogWarning("No Wise Man NPC found on map for player {Player}", playerState.Character.Name);
                return false;
            }
            // Check proximity (within 5 tiles)
            var dx = Math.Abs(playerState.Character.X - wiseManNpc.X);
            var dy = Math.Abs(playerState.Character.Y - wiseManNpc.Y);
            const int maxDistance = 5;
            if (dx > maxDistance || dy > maxDistance)
            {
                _logger.LogWarning("Player {Player} is not in range of Wise Man NPC", playerState.Character.Name);
                return false;
            }

            _logger.LogInformation("Player {Player} asks Wise Man: {Query}", 
                playerState.Character.Name, query);

            var request = new WiseManRequest(playerState, query, wiseManNpc);
            if (_queueService.TryEnqueue(request))
            {
                _logger.LogDebug("Queued Wise Man request from {Player}", playerState.Character.Name);
            }
            else
            {
                _logger.LogWarning("Failed to queue Wise Man request - queue full or could not find wise man");
            }
            return true;
        }
        return false;
    }
}
