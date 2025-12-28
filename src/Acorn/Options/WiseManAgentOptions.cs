namespace Acorn.Options;

/// <summary>
/// Configuration options for Wise Man Agent (Gemini AI integration).
/// </summary>
public class WiseManAgentOptions
{
    /// <summary>
    /// The Gemini API key. Store this in user secrets.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The Gemini model to use.
    /// </summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    /// <summary>
    /// Maximum characters for the response.
    /// </summary>
    public int MaxResponseLength { get; set; } = 600;

    /// <summary>
    /// Whether the Wise Man Agent integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The section name in configuration.
    /// </summary>
    public static string SectionName => "WiseManAgent";
}
