namespace Acorn.Options;

public class HostingOptions
{
    public required SLNOptions SLN { get; set; }
    public required string HostName { get; set; }
    public required int Port { get; set; }
    public required int WebSocketPort { get; set; }

    public static string SectionName => "Hosting";
}