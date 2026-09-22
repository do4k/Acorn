namespace Acorn.Options;

/// <summary>
///     Configuration options for the Acornbot PM service. Acornbot is a pseudo
///     character players can whisper ("PM") to run a curated set of self-service
///     commands, e.g. <c>!acornbot title Cool Dude</c> from local chat.
/// </summary>
public class AcornbotOptions
{
    /// <summary>
    ///     Whether Acornbot accepts whispers. When disabled, PMs addressed to the
    ///     bot name are rejected like any unknown player.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     The in-game name players whisper to reach the bot. Matched
    ///     case-insensitively (the eoweb client lower-cases whisper targets).
    /// </summary>
    public string Name { get; set; } = "Acornbot";

    /// <summary>
    ///     Options for the <c>title</c> command.
    /// </summary>
    public AcornbotTitleOptions Title { get; set; } = new();

    /// <summary>
    ///     The section name in configuration.
    /// </summary>
    public static string SectionName => "Acornbot";
}
