namespace Acorn.World.Services.Party;

/// <summary>
/// Represents an active party of players.
/// </summary>
public class Party
{
    private readonly Lock _lock = new();
    private readonly List<int> _memberSessionIds;

    public int LeaderSessionId { get; private set; }

    public List<int> Members
    {
        get
        {
            lock (_lock) return [.. _memberSessionIds];
        }
    }

    public Party(int leaderSessionId, int memberSessionId)
    {
        LeaderSessionId = leaderSessionId;
        _memberSessionIds = [leaderSessionId, memberSessionId];
    }

    public bool ContainsMember(int sessionId)
    {
        lock (_lock) return _memberSessionIds.Contains(sessionId);
    }

    public void AddMember(int sessionId)
    {
        lock (_lock)
        {
            if (!_memberSessionIds.Contains(sessionId))
            {
                _memberSessionIds.Add(sessionId);
            }
        }
    }

    public void RemoveMember(int sessionId)
    {
        lock (_lock)
        {
            _memberSessionIds.Remove(sessionId);

            if (sessionId == LeaderSessionId && _memberSessionIds.Count > 0)
            {
                LeaderSessionId = _memberSessionIds[0];
            }
        }
    }

    public int MemberCount
    {
        get { lock (_lock) return _memberSessionIds.Count; }
    }
}
