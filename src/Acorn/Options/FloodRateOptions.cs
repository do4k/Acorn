namespace Acorn.Options;

/// <summary>
///     Admin command flood rates reported to the client in <c>Welcome_Reply</c> server
///     settings. A value of 0 disables flood protection for that admin tier.
/// </summary>
public class FloodRateOptions
{
    public int SpyAndLightGuide { get; set; } = 10;
    public int Guardian { get; set; } = 10;
    public int GameMaster { get; set; } = 10;
    public int HighGameMaster { get; set; }
}
