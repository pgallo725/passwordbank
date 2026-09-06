using System.Security.Cryptography;

namespace PasswordBank.Cryptography
{
    internal class KeyGenerator(byte[] salt)
    {
        public void GetKey(string password, Span<byte> destination)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);

            if (destination.Length != AES256.KeySize)
                throw new ArgumentException($"The destination buffer must be exactly {AES256.KeySize} bytes large.");

            PBKDF2.GenerateKey(password, salt, destination);
        }
    }
}
