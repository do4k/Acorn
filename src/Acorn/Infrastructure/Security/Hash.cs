using System.Security.Cryptography;

namespace Acorn.Infrastructure.Security;

public static class Hash
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int LegacyIterations = 10000;
    private const string VersionPrefix = "$pbkdf2-sha256$";

    /// <summary>
    ///     Hashes a password with the given PBKDF2 iteration count. The returned salt
    ///     is versioned ($pbkdf2-sha256$&lt;iterations&gt;$&lt;base64&gt;) so verification
    ///     stays backward compatible when the iteration count changes.
    /// </summary>
    public static string HashPassword(string username, string password, int iterations, out string salt)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(username + password, saltBytes, iterations,
            HashAlgorithmName.SHA256, HashSize);
        salt = $"{VersionPrefix}{iterations}${Convert.ToBase64String(saltBytes)}";
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    ///     Verifies a password against a stored hash. Accepts legacy salts (plain
    ///     base64, 10k iterations) as well as versioned salts.
    /// </summary>
    public static bool VerifyPassword(string username, string password, string salt, string storedHash)
    {
        int iterations;
        byte[] saltBytes;
        byte[] storedBytes;

        try
        {
            (iterations, saltBytes) = ParseSalt(salt);
            storedBytes = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var hash = Rfc2898DeriveBytes.Pbkdf2(username + password, saltBytes, iterations,
            HashAlgorithmName.SHA256, HashSize);

        return hash.Length == storedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(hash, storedBytes);
    }

    private static (int iterations, byte[] saltBytes) ParseSalt(string salt)
    {
        if (salt.StartsWith(VersionPrefix, StringComparison.Ordinal))
        {
            var parts = salt.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && parts[0] == "pbkdf2-sha256" &&
                int.TryParse(parts[1], out var iterations) && iterations > 0)
            {
                return (iterations, Convert.FromBase64String(parts[2]));
            }
        }

        // Legacy format: plain base64 salt, 10k iterations.
        return (LegacyIterations, Convert.FromBase64String(salt));
    }
}
