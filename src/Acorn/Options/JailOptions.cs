namespace Acorn.Options;

/// <summary>
///     Location of the jail map used by admin commands and reported to the client in
///     <c>Welcome_Reply</c> server settings. Falls back to the rescue/new-character
///     location when not configured.
/// </summary>
public class JailOptions
{
    public int Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}
