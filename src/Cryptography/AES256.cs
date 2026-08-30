using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace PasswordBank.Cryptography
{
    /// <summary>
    /// A static utility class that provides functions to encrypt and decrypt data using the AES-256-GCM cryptographic algorithm.
    /// </summary>
    internal class AES256
    {
        private const int KeySize = 32; /* 256 bits */
        private const int NonceSize = 12; /* 96 bits */
        private const int TagSize = 16; /* 128 bits */

        public static (byte[], byte[]) Encrypt(string plaintext, byte[] key, byte[] nonce)
        {
            ArgumentException.ThrowIfNullOrEmpty(plaintext);
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(nonce);

            if (key.Length != KeySize)
                throw new ArgumentException($"An AES-256 key must be exactly {KeySize} bytes long.");
            if (nonce.Length != NonceSize)
                throw new ArgumentException($"An AES-256-GCM nonce must be exactly {NonceSize} bytes long.");

            byte[] bytes = Encoding.Unicode.GetBytes(plaintext);
            byte[] authenticationTag = GC.AllocateUninitializedArray<byte>(TagSize);

            using var aes256gcm = new AesGcm(key, TagSize);
            aes256gcm.Encrypt(nonce, bytes, bytes, authenticationTag);  // use the same buffer as input and output

            return (bytes /* ciphertext */, authenticationTag);
        }

        public static string Decrypt(byte[] ciphertext, byte[] key, byte[] nonce, byte[] tag)
        {
            ArgumentNullException.ThrowIfNull(ciphertext);
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(nonce);
            ArgumentNullException.ThrowIfNull(tag);

            if (key.Length != KeySize)
                throw new ArgumentException($"An AES-256 key must be exactly {KeySize} bytes long.");
            if (nonce.Length != NonceSize)
                throw new ArgumentException($"An AES-256-GCM nonce must be exactly {NonceSize} bytes long.");
            if (tag.Length != TagSize)
                throw new ArgumentException($"An AES-256-GCM tag must be exactly {TagSize} bytes long.");

            // Allocate decryption buffer from a pool, since it's temporary
            byte[] buffer = ArrayPool<byte>.Shared.Rent(ciphertext.Length);
            try
            {
                using var aes256gcm = new AesGcm(key, TagSize);
                aes256gcm.Decrypt(nonce, ciphertext, tag, buffer.AsSpan(0, ciphertext.Length));

                string plaintext = Encoding.Unicode.GetString(buffer.AsSpan(0, ciphertext.Length));
                return plaintext;
            }
            finally
            {
                // Reset the memory before returning it to the pool, to avoid leaking decrypted data
                CryptographicOperations.ZeroMemory(buffer);
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
