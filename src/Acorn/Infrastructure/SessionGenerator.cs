using Moffat.EndlessOnline.SDK.Data;

namespace Acorn.Infrastructure;

public class SessionGenerator : ISessionGenerator
{
    private readonly Random _rnd = new();

    // Session IDs 1-7 collide with AccountReply enum values (Exists=1, NotApproved=2,
    // Created=3, ChangeFailed=5, Changed=6, RequestDenied=7). The client uses the
    // ReplyCode to distinguish responses, so session IDs must avoid these values.
    private const int MinSessionId = 8;

    public int Generate()
    {
        return _rnd.Next(MinSessionId, (int)EoNumericLimits.SHORT_MAX);
    }
}

public interface ISessionGenerator
{
    int Generate();
}