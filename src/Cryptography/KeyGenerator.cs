using System.Security.Cryptography;

namespace PasswordBank.Cryptography
{
    internal class KeyGenerator(byte[] salt)
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
