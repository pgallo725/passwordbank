using System.Security.Cryptography;
using System.Text;

namespace PasswordBank.Cryptography
{
    /// <summary>
    /// A static utility class that provides functions to encrypt and decrypt data using the AES-256-GCM cryptographic algorithm.
    /// </summary>
    internal class AES256
    {
        public static (byte[], byte[]) Encrypt(string plaintext, byte[] key, byte[] nonce)
        {
            ArgumentException.ThrowIfNullOrEmpty(plaintext);
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(nonce);

            if (key.Length != 32 /* 256 bits */)
                throw new ArgumentException("An AES-256 key must be exactly 32 bytes long.");
            if (nonce.Length != 12 /* 96 bits */)
                throw new ArgumentException("An AES-256-GCM nonce must be exactly 12 bytes long.");

            byte[] plaintextBytes = Encoding.Unicode.GetBytes(plaintext);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] authenticationTag = new byte[16];

            using var aes256gcm = new AesGcm(key, 16);
            aes256gcm.Encrypt(nonce, plaintextBytes, ciphertext, authenticationTag);

            return (ciphertext, authenticationTag);
        }

        public static string Decrypt(byte[] ciphertext, byte[] key, byte[] nonce, byte[] tag)
        {
            ArgumentNullException.ThrowIfNull(ciphertext);
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(nonce);
            ArgumentNullException.ThrowIfNull(tag);

            if (key.Length != 32 /* 256 bits */)
                throw new ArgumentException("An AES-256 key must be exactly 32 bytes long.");
            if (nonce.Length != 12 /* 96 bits */)
                throw new ArgumentException("An AES-256-GCM nonce must be exactly 12 bytes long.");
            if (tag.Length != 16)
                throw new ArgumentException("An AES-256-GCM tag must be exactly 16 bytes long.");

            byte[] plaintextBytes = new byte[ciphertext.Length];

            using var aes256gcm = new AesGcm(key, 16);
            aes256gcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

            string plaintext = Encoding.Unicode.GetString(plaintextBytes);
            return plaintext;
        }
    }
}
