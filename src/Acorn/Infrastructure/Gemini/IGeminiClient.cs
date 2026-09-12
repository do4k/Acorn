using Refit;

namespace Acorn.Infrastructure.Gemini;

/// <summary>
///     Refit client for the Gemini API.
/// </summary>
public interface IGeminiClient
{
    [Post("/v1beta/models/{model}:generateContent")]
    Task<GeminiResponse> GenerateContentAsync(
        string model,
        [Body] GeminiRequest request,
        [Header("X-goog-api-key")] string apiKey);
}
