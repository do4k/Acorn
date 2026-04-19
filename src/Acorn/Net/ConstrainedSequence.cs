using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;

namespace Acorn.Net;

/// <summary>
/// Generates sequence start values constrained to fit within a single EO char (0-252).
/// The EO protocol transmits sequences as single-byte chars. The SDK's default Generate()
/// can produce values up to 1756, which overflow when encoded with AddChar(). Following
/// reoserv's approach, we constrain start values to [0, 243] so that with the counter
/// cycling 0-9, the maximum sequence value is 252 (CHAR_MAX).
/// </summary>
public static class ConstrainedSequence
{
    /// <summary>
    /// Maximum sequence start value. With counter range 0-9, max sequence = 243 + 9 = 252 = CHAR_MAX.
    /// </summary>
    private const int MaxStartValue = (int)EoNumericLimits.CHAR_MAX - 9;

    /// <summary>
    /// Generates an <see cref="InitSequenceStart"/> with Value in [0, 243].
    /// Computes valid seq1/seq2 encoding: value = seq1 * 7 + seq2 - 13.
    /// </summary>
    public static InitSequenceStart GenerateInitStart(Random rnd)
    {
        var value = rnd.Next(MaxStartValue + 1);
        var encoded = value + 13;
        var seq1 = encoded / 7;
        var seq2 = encoded % 7;
        return InitSequenceStart.FromInitValues(seq1, seq2);
    }

    /// <summary>
    /// Generates a <see cref="PingSequenceStart"/> with Value in [0, 243].
    /// Computes valid seq1/seq2 encoding: value = seq1 - seq2.
    /// </summary>
    public static PingSequenceStart GeneratePingStart(Random rnd)
    {
        var value = rnd.Next(MaxStartValue + 1);
        var maxSeq2 = (int)EoNumericLimits.CHAR_MAX - value;
        var seq2 = rnd.Next(maxSeq2 + 1);
        var seq1 = value + seq2;
        return PingSequenceStart.FromPingValues(seq1, seq2);
    }
}
