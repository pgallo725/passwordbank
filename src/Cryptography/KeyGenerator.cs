using System.Security.Cryptography;

namespace PasswordBank.Cryptography
{
    /// <summary>
    /// A simple value type that can be passed around to generate an encryption key from a password on-the-fly.
    /// </summary>
    internal readonly ref struct KeyGenerator(byte[] salt)
    {
        public void GetKey(ReadOnlySpan<char> password, Span<byte> destination)
        {
            if (password.IsEmpty)
                throw new ArgumentException("Value cannot be an empty ReadOnlySpan<char>.", nameof(password));

            if (destination.Length != AES256.KeySize)
                throw new ArgumentException($"The destination buffer must be exactly {AES256.KeySize} bytes large.");

            PBKDF2.GenerateKey(password, salt, destination);
        }
    }
}
