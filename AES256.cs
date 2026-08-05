using System.Security.Cryptography;

namespace passwordbank
{
    /// <summary>
    /// A static utility class that provides functions to encrypt and decrypt data using the AES-256 cryptographic algorithm.
    /// NOTE: the code for this class is taken from the Microsoft sample code (with some modifications):
    ///   https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes?view=net-10.0
    /// </summary>
    internal class AES256
    {
        public static byte[] Encrypt(string plainText, byte[] key, byte[] iv)
        {
            ArgumentException.ThrowIfNullOrEmpty(plainText);
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(iv);

            if (key.Length != 32 /* 256 bits */)
                throw new ArgumentException("An AES-256 key must be exactly 32 bytes long.");
            if (iv.Length != 16)
                throw new ArgumentException("An AES-256 initialization vector must be exactly 16 bytes long.");

            byte[] encrypted;
            using (Aes algorithm = Aes.Create())
            {
                algorithm.Key = key;
                algorithm.IV = iv;

                // Create an encryptor to perform the stream transform
                ICryptoTransform encryptor = algorithm.CreateEncryptor();

                // Create the streams used for encryption
                using MemoryStream msEncrypt = new();
                using CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write);
                using StreamWriter swEncrypt = new(csEncrypt);

                // Write all data to the stream
                swEncrypt.Write(plainText);
                swEncrypt.Flush();
                csEncrypt.FlushFinalBlock();
                encrypted = msEncrypt.ToArray();
            }

            return encrypted;
        }

        public static string Decrypt(byte[] cipherText, byte[] key, byte[] iv)
        {
            ArgumentNullException.ThrowIfNull(cipherText);
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(iv);

            if (key.Length != 32 /* 256 bits */)
                throw new ArgumentException("An AES-256 key must be exactly 32 bytes long.");
            if (iv.Length != 16)
                throw new ArgumentException("An AES-256 initialization vector must be exactly 16 bytes long.");

            string plaintext = string.Empty;
            using (Aes algorithm = Aes.Create())
            {
                algorithm.Key = key;
                algorithm.IV = iv;

                // Create a decryptor to perform the stream transform
                ICryptoTransform decryptor = algorithm.CreateDecryptor();

                // Create the streams used for decryption
                using MemoryStream msDecrypt = new(cipherText);
                using CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read);
                using StreamReader srDecrypt = new(csDecrypt);

                // Read the decrypted bytes from the stream into a string
                plaintext = srDecrypt.ReadToEnd();
            }

            return plaintext;
        }
    }
}
