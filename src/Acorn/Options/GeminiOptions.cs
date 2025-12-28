namespace Acorn.Options;

/// <summary>
/// Configuration options for Gemini AI integration.
/// </summary>
public class GeminiOptions
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
    public int MaxResponseLength { get; set; } = 200;
    
    /// <summary>
    /// Whether the Gemini integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

