namespace Acorn.Shared.Models.Online;

/// <summary>
///     Shared presentation metadata for how Acornbot advertises itself in the
///     online-players list. The title doubles as the discovery hint players can
///     use in the whisper channel.
/// </summary>
public static class AcornbotPresence
{
    /// <summary>
    ///     The title Acornbot carries while it is online, teaching players how
    ///     to reach it.
    /// </summary>
    public const string OnlineListTitle = "!acornbot help";
}
