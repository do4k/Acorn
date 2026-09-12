using Refit;

namespace Acorn.Infrastructure.Gemini;

public class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
    public string? FinishReason { get; set; }
}
