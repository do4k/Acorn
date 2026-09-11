using SdkVersion = Moffat.EndlessOnline.SDK.Protocol.Net.Version;

namespace Acorn.Net;

/// <summary>
///     Validates the client version reported during the Init handshake against the
///     configured minimum/maximum supported versions.
/// </summary>
public static class ClientVersionValidator
{
    /// <summary>
    ///     Returns true when <paramref name="version" /> falls within the inclusive
    ///     range defined by <paramref name="minVersion" /> and <paramref name="maxVersion" />.
    /// </summary>
    /// <remarks>
    ///     A malformed configuration fails open (returns true) so a typo cannot lock
    ///     every client out. A <paramref name="maxVersion" /> of "-1" or empty accepts
    ///     any version at or above the minimum.
    /// </remarks>
    public static bool IsSupported(SdkVersion version, string minVersion, string maxVersion)
    {
        var actual = new Version(version.Major, version.Minor, version.Patch);

        if (!Version.TryParse(minVersion, out var min))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(maxVersion) || maxVersion == "-1")
        {
            return actual >= min;
        }

        if (!Version.TryParse(maxVersion, out var max))
        {
            return true;
        }

        return actual >= min && actual <= max;
    }
}
