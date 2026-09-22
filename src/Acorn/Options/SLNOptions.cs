namespace Acorn.Options;

public class SLNOptions
{
    public required bool Enabled { get; set; }

    /// <summary>
    ///     Base URL of the Server Link Network endpoint, e.g. https://apollo-games.com/SLN/sln.php.
    ///     The endpoint's certificate only covers the apollo-games.com apex (no www), and the
    ///     plaintext http URL merely 301-redirects to https, so https on the apex is used.
    /// </summary>
    public required string Url { get; set; }

    public required int PingRate { get; set; }
    public required string UserAgent { get; set; }
    public required string Zone { get; set; }
    public required string ServerName { get; set; }
    public required string Site { get; set; }

    /// <summary>
    ///     Maximum duration, in seconds, allowed for a single SLN status check request.
    ///     Without an explicit timeout a hung endpoint holds the check task for the
    ///     <see cref="HttpClient" /> default of 100 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    public static string SectionName => "SLN";
}