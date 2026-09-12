using Refit;

namespace Acorn.Infrastructure.Gemini;

public class GenerationConfig
{
    public int? MaxOutputTokens { get; set; }
    public double? Temperature { get; set; }
}
