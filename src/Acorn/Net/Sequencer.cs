namespace Acorn.Net;

/// <summary>
/// Packet sequencer matching eolib-rs (reoserv) behavior.
///
/// Uses PRE-increment: counter advances before computing the return value.
/// Sequence: start+1, start+2, ..., start+9, start+0, start+1, ...
///
/// This differs from eolib-dotnet's PacketSequencer which uses POST-increment
/// (start+0, start+1, ...). The pre-increment behavior naturally compensates for
/// the server skipping Init_Init sequencing when the client is Uninitialized.
/// </summary>
public sealed class Sequencer
{
    private int _start;
    private int _counter;

    public Sequencer(int start)
    {
        _start = start;
        _counter = 0;
    }

    /// <summary>
    /// Returns the next sequence value using pre-increment (matches eolib-rs).
    /// Counter advances first, then value is computed as start + counter.
    /// </summary>
    public int NextSequence()
    {
        _counter = (_counter + 1) % 10;
        return _start + _counter;
    }

    /// <summary>
    /// Sets a new starting value without resetting the counter (matches eolib-rs set_start).
    /// </summary>
    public void SetStart(int start)
    {
        _start = start;
    }

    /// <summary>
    /// Gets the current starting value (matches eolib-rs get_start).
    /// </summary>
    public int GetStart() => _start;
}
