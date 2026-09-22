namespace Acorn.Api.Options;

/// <summary>
///     The API's view of the Acornbot configuration: it only needs to know whether
///     the whisper bot is enabled and what to call it, to advertise it in the
///     online-players list. Bound from the shared "Acornbot" configuration section;
///     docker-compose keeps the server and API in sync via the Acornbot__* overrides.
/// </summary>
public class AcornbotOptions
{
    /// <summary>
    ///     Whether Acornbot is enabled on the game server.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     The bot's in-game name.
    /// </summary>
    public string Name { get; set; } = "Acornbot";

    /// <summary>
    ///     The section name in configuration.
    /// </summary>
    public static string SectionName => "Acornbot";
}
