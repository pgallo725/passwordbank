using System.Buffers;
using System.Security.Cryptography;
using PasswordBank.Cryptography;

namespace PasswordBank.DataModel
{
    internal record PlaintextCredential : IDisposable
    {
        public required string       Name         { get; set; }
        public required string?      EmailAddress { get; set; }
        public required string?      Username     { get; set; }
        public required SecretString Password     { get; set; }

        public EncryptedCredential Encrypt(ReadOnlySpan<byte> key)
        {
            byte[] nonce = Cryptography.Random.GenerateBytes(AES256.NonceSize);
            (byte[] ciphertext, byte[] tag) = AES256.Encrypt(Password.GetData(), key, nonce);

            return new EncryptedCredential()
            {
                Name = Name,
                EmailAddress = EmailAddress,
                Username = Username,
                EncryptionNonce = nonce,
                EncryptedPassword = ciphertext,
                EncryptionTag = tag
            };
        }

        public EncryptedCredential Encrypt(KeyGenerator keygen, SecretString password)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AES256.KeySize);
            try
            {
                // Generate encryption key from the master password
                Span<byte> key = buffer.AsSpan(0, AES256.KeySize);
                keygen.GetKey(password.GetData(), key);

                byte[] nonce = Cryptography.Random.GenerateBytes(AES256.NonceSize);
                (byte[] ciphertext, byte[] tag) = AES256.Encrypt(Password.GetData(), key, nonce);

                return new EncryptedCredential()
                {
                    Name = Name,
                    EmailAddress = EmailAddress,
                    Username = Username,
                    EncryptionNonce = nonce,
                    EncryptedPassword = ciphertext,
                    EncryptionTag = tag
                };
            }
            finally
            {
                CryptographicOperations.ZeroMemory(buffer);  // avoid leaking the encryption key
                ArrayPool<byte>.Shared.Return(buffer);       // return memory to the pool
            }
        }

        public void Dispose()
        {
            // Wipe the plaintext password from memory
            Password.Zeroise();
        }
    }


    internal record EncryptedCredential
    {
        public required string  Name              { get; set; }
        public required string? EmailAddress      { get; set; }
        public required string? Username          { get; set; }
        public required byte[]  EncryptionNonce   { get; set; }
        public required byte[]  EncryptedPassword { get; set; }
        public required byte[]  EncryptionTag     { get; set; }

        public PlaintextCredential Decrypt(ReadOnlySpan<byte> key)
        {
            return new PlaintextCredential()
            {
                Name = Name,
                EmailAddress = EmailAddress,
                Username = Username,
                Password = new(AES256.Decrypt(EncryptedPassword, key, EncryptionNonce, EncryptionTag)),
            };
        }

        public PlaintextCredential Decrypt(KeyGenerator keygen, SecretString password)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AES256.KeySize);
            try
            {
                // Generate decryption key from the master password
                Span<byte> key = buffer.AsSpan(0, AES256.KeySize);
                keygen.GetKey(password.GetData(), key);

                return new PlaintextCredential()
                {
                    Name = Name,
                    EmailAddress = EmailAddress,
                    Username = Username,
                    Password = new(AES256.Decrypt(EncryptedPassword, key, EncryptionNonce, EncryptionTag)),
                };
            }
            finally
            {
                CryptographicOperations.ZeroMemory(buffer);  // avoid leaking the decryption key
                ArrayPool<byte>.Shared.Return(buffer);       // return memory to the pool
            }
        }
    }
}
