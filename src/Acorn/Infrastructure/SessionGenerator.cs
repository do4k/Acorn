using Moffat.EndlessOnline.SDK.Data;

namespace Acorn.Infrastructure;

public class SessionGenerator : ISessionGenerator
{
    private readonly Random _rnd = new();
    
    // Reserve IDs 50001-64008 for bots, use 1-50000 for real players
    private const int MAX_PLAYER_SESSION_ID = 50000;

    public int Generate()
    {
        return _rnd.Next(1, MAX_PLAYER_SESSION_ID + 1);
    }
}

public interface ISessionGenerator
{
    int Generate();
}