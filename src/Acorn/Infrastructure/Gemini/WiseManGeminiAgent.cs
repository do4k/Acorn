using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Infrastructure.Gemini;

/// <summary>
/// Service for generating AI responses using Gemini.
/// </summary>
public interface IWiseManAgent
{
    /// <summary>
    /// Generate a response from the Wise Man NPC.
    /// </summary>
    Task<string?> GetWiseManResponseAsync(string playerName, string query);
}

public class WiseManGeminiAgent : IWiseManAgent
{
    private readonly IGeminiClient _geminiClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<WiseManGeminiAgent> _logger;

    private const string SystemPrompt = """
        You are "The Wise Man", an ancient and mysterious NPC in a fantasy MMORPG called Endless Online.
        You speak in a wise, cryptic, but helpful manner. You are knowledgeable about the game world,
        quests, items, and give advice to adventurers. Keep your responses short, mystical, and in-character.
        Never break character. Never mention you are an AI. Speak as if you are truly an ancient sage
        who has lived for centuries in this fantasy world. Use archaic language occasionally.
        Your responses must be concise (under 200 characters if possible) as they appear in game chat.
        """;

    public WiseManGeminiAgent(
        IGeminiClient geminiClient,
        IOptions<GeminiOptions> options,
        ILogger<WiseManGeminiAgent> logger)
    {
        _geminiClient = geminiClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetWiseManResponseAsync(string playerName, string query)
    {
        if (!_options.Enabled || string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("Gemini is not configured or disabled");
            return null;
        }

        try
        {
            var request = new GeminiRequest
            {
                Contents =
                [
                    new GeminiContent
                    {
                        Role = "user",
                        Parts = [new GeminiPart { Text = $"{SystemPrompt}\n\nA player named '{playerName}' approaches you and asks: \"{query}\"\n\nRespond in character as The Wise Man:" }]
                    }
                ],
                GenerationConfig = new GenerationConfig
                {
                    MaxOutputTokens = 100,
                    Temperature = 0.7
                }
            };

            var response = await _geminiClient.GenerateContentAsync(_options.Model, request, _options.ApiKey);

            var text = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("Gemini returned empty response");
                return null;
            }

            // Truncate if too long for game chat
            if (text.Length > _options.MaxResponseLength)
            {
                text = text[.._options.MaxResponseLength] + "...";
            }

            _logger.LogInformation("Wise Man responds to {Player}: {Response}", playerName, text);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Gemini response for player {Player}", playerName);
            return null;
        }
    }
}

