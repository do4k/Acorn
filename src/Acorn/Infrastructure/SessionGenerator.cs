using Moffat.EndlessOnline.SDK.Data;

namespace Acorn.Infrastructure;

public class SessionGenerator : ISessionGenerator
{
    private readonly Random _rnd = new();

    public int Generate()
    {
        return _rnd.Next(1, (int)EoNumericLimits.SHORT_MAX);
    }
}

public interface ISessionGenerator
{
    int Generate();
}