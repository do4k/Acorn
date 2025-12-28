using Refit;

namespace Acorn.Infrastructure.Gemini;

/// <summary>
/// Refit client for the Gemini API.
/// </summary>
public interface IGeminiClient
{
    [Post("/v1beta/models/{model}:generateContent")]
    Task<GeminiResponse> GenerateContentAsync(
        string model,
        [Body] GeminiRequest request,
        [Header("X-goog-api-key")] string apiKey);
}

#region Request/Response Models

public class GeminiRequest
{
    public List<GeminiContent> Contents { get; set; } = [];
    public GenerationConfig? GenerationConfig { get; set; }
}

public class GeminiContent
{
    public List<GeminiPart> Parts { get; set; } = [];
    public string? Role { get; set; }
}

public class GeminiPart
{
    public string Text { get; set; } = string.Empty;
}

public class GenerationConfig
{
    public int? MaxOutputTokens { get; set; }
    public double? Temperature { get; set; }
}

public class GeminiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
}

public class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
    public string? FinishReason { get; set; }
}

#endregion

