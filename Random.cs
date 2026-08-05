using System.Security.Cryptography;

namespace passwordbank
{
    internal class Random
    {
        public static byte[] GenerateBytes(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

            return RandomNumberGenerator.GetBytes(length);
        }
    }
}
