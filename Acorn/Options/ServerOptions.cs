namespace Acorn.Options;

public class ServerOptions
{
    public NewCharacterOptions NewCharacter { get; set; } = new();
    public required string ServerName { get; set; }
    public required string Site { get; set; }
    public required string Hostname { get; set; }
    public required int Port { get; set; }
}