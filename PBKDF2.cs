using System.Security.Cryptography;

namespace passwordbank
{
    internal class PBKDF2
    {
        public static byte[] GenerateKey(string password, byte[] salt)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);
            ArgumentNullException.ThrowIfNull(salt);

            if (salt.Length < 16)
                throw new ArgumentException("PBKDF2 requires at least 16 bytes of salt", nameof(salt));

            return Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                300000 /* iterations */,
                HashAlgorithmName.SHA512,
                32 /* 256-bit key for AES-256 */
            );
        }
    }
}
