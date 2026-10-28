using PasswordBank.Cryptography;
using PasswordBank.Foundation;

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
            using PooledArray<byte> key = new(AES256.KeySize);

            // Generate encryption key from the master password
            keygen.GetKey(password.GetData(), key.GetData_W());

            byte[] nonce = Cryptography.Random.GenerateBytes(AES256.NonceSize);
            (byte[] ciphertext, byte[] tag) = AES256.Encrypt(Password.GetData(), key.GetData(), nonce);

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
            using PooledArray<byte> key = new(AES256.KeySize);

            // Generate decryption key from the master password
            keygen.GetKey(password.GetData(), key.GetData_W());

            return new PlaintextCredential()
            {
                Name = Name,
                EmailAddress = EmailAddress,
                Username = Username,
                Password = new(AES256.Decrypt(EncryptedPassword, key.GetData(), EncryptionNonce, EncryptionTag)),
            };
        }
    }
}
