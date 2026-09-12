using Refit;

namespace Acorn.Infrastructure.Gemini;

public class GeminiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
}
