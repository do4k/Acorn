using Refit;

namespace Acorn.Infrastructure.Gemini;

public class GeminiRequest
{
    public List<GeminiContent> Contents { get; set; } = [];
    public GenerationConfig? GenerationConfig { get; set; }
}
