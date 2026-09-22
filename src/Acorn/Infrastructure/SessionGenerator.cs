using Acorn.World;
using Moffat.EndlessOnline.SDK.Data;

namespace Acorn.Infrastructure;

/// <summary>
///     Issues session ids by walking a monotonic, wrapping counter and skipping ids that are
///     still registered in the world state. Unlike random ids, sequential allocation makes id
///     reuse take a full 65k-connection cycle, and the in-use check means the (already rare)
///     collision is resolved here instead of by <c>TryAddPlayer</c> failing later.
/// </summary>
public class SessionGenerator(WorldState worldState) : ISessionGenerator
{
    // Session IDs 1-7 collide with AccountReply enum values (Exists=1, NotApproved=2,
    // Created=3, ChangeFailed=5, Changed=6, RequestDenied=7). The client uses the
    // ReplyCode to distinguish responses, so session IDs must avoid these values.
    private const int MinSessionId = 8;
    private const int MaxSessionId = (int)EoNumericLimits.SHORT_MAX - 1;

    private int _lastIssued = MinSessionId - 1;

    public int Generate()
    {
        var range = MaxSessionId - MinSessionId + 1;
        for (var attempt = 0; attempt < range; attempt++)
        {
            var candidate = NextCandidate();
            if (!worldState.IsSessionInUse(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No free session ids are available.");
    }

    private int NextCandidate()
    {
        while (true)
        {
            var last = Volatile.Read(ref _lastIssued);
            var candidate = last >= MaxSessionId ? MinSessionId : last + 1;
            if (Interlocked.CompareExchange(ref _lastIssued, candidate, last) == last)
            {
                return candidate;
            }
        }
    }
}
