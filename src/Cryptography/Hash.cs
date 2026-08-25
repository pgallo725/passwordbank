using System.Security.Cryptography;

namespace PasswordBank.Cryptography
{
    /// <summary>
    /// A static utility class with cryptographically-safe hash-related functions.
    /// </summary>
    internal class Hash
    {
        public static bool Equals(byte[] firstHash, byte[] secondHash)
        {
            ArgumentNullException.ThrowIfNull(firstHash);
            ArgumentNullException.ThrowIfNull(secondHash);

            // Use fixed-time comparison function to avoid any timing side-channel information leak
            return CryptographicOperations.FixedTimeEquals(firstHash, secondHash);
        }
    }
}
