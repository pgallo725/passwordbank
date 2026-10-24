using System.Security.Cryptography;

namespace PasswordBank.Cryptography
{
    /// <summary>
    /// A static utility class that provides a function to generate an encryption key<br/>
    ///  using the PBKDF2 key derivation method with some pre-defined parameters.
    /// </summary>
    internal class PBKDF2
    {
        public const int SaltSize = 16; /* 16 bytes */
        public const int OutputSize = 32; /* 256 bits */

        public static void GenerateKey(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt, Span<byte> destination)
        {
            if (password.IsEmpty)
                throw new ArgumentException("Value cannot be an empty ReadOnlySpan<char>.", nameof(password));

            if (salt.Length < SaltSize)
                throw new ArgumentException($"PBKDF2 requires at least {SaltSize} bytes of salt", nameof(salt));

            if (destination.Length != OutputSize)
                throw new ArgumentException($"PBKDF2 requires exactly {OutputSize} bytes in the output buffer", nameof(destination));

            Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                destination,
                300000 /* iterations */,
                HashAlgorithmName.SHA512
            );
        }
    }
}
