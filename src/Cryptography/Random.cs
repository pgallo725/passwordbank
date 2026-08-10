using System.Security.Cryptography;

namespace PasswordBank.Cryptography
{
    /// <summary>
    /// A static utility class with cryptographically-safe random number generation functions.
    /// </summary>
    internal class Random
    {
        public static byte[] GenerateBytes(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

            return RandomNumberGenerator.GetBytes(length);
        }
    }
}
