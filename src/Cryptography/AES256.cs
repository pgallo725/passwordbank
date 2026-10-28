using PasswordBank.Foundation;
using System.Security.Cryptography;
using System.Text;

namespace PasswordBank.Cryptography
{
    /// <summary>
    /// A static utility class that provides functions to encrypt and decrypt data using the AES-256-GCM cryptographic algorithm.
    /// </summary>
    internal static class AES256
    {
        public const int KeySize = 32; /* 256 bits */
        public const int NonceSize = 12; /* 96 bits */
        public const int TagSize = 16; /* 128 bits */

        public static (byte[], byte[]) Encrypt(ReadOnlySpan<char> plaintext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce)
        {
            if (key.Length != KeySize)
                throw new ArgumentException($"An AES-256 key must be exactly {KeySize} bytes long.");

            if (nonce.Length != NonceSize)
                throw new ArgumentException($"An AES-256-GCM nonce must be exactly {NonceSize} bytes long.");

            int nBytes = Encoding.Unicode.GetByteCount(plaintext);
            byte[] bytes = GC.AllocateUninitializedArray<byte>(nBytes); 
            byte[] authenticationTag = GC.AllocateUninitializedArray<byte>(TagSize);
            Encoding.Unicode.GetBytes(plaintext, bytes);

            using var aes256gcm = new AesGcm(key, TagSize);
            aes256gcm.Encrypt(nonce, bytes, bytes, authenticationTag);  // use the same buffer as input and output

            return (bytes /* ciphertext */, authenticationTag);
        }

        public static char[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag)
        {
            if (key.Length != KeySize)
                throw new ArgumentException($"An AES-256 key must be exactly {KeySize} bytes long.");

            if (nonce.Length != NonceSize)
                throw new ArgumentException($"An AES-256-GCM nonce must be exactly {NonceSize} bytes long.");

            if (tag.Length != TagSize)
                throw new ArgumentException($"An AES-256-GCM tag must be exactly {TagSize} bytes long.");

            // Allocate decryption buffer from a pool, since it's temporary
            using PooledArray<byte> buffer = new(ciphertext.Length);

            using var aes256gcm = new AesGcm(key, TagSize);
            aes256gcm.Decrypt(nonce, ciphertext, tag, buffer.GetData_W());

            int nChars = Encoding.Unicode.GetCharCount(buffer.GetData());
            char[] plaintext = GC.AllocateUninitializedArray<char>(nChars);
            Encoding.Unicode.GetChars(buffer.GetData(), plaintext);

            return plaintext;
        }
    }
}
