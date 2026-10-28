using System.Security.Cryptography;

namespace PasswordBank.Cryptography
{
    /// <summary>
    /// A static utility class with cryptographically-safe hash-related functions.
    /// </summary>
    internal static class Hash
    {
        public static bool Equals(ReadOnlySpan<byte> firstHash, ReadOnlySpan<byte> secondHash)
        {
            // Use fixed-time comparison function to avoid any timing side-channel information leak
            return CryptographicOperations.FixedTimeEquals(firstHash, secondHash);
        }
    }
}
