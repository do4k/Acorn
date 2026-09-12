using Refit;

namespace Acorn.Infrastructure.Gemini;

public class GeminiContent
{
    public List<GeminiPart> Parts { get; set; } = [];
    public string? Role { get; set; }
}
