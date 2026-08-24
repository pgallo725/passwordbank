using PasswordBank.Cryptography;

namespace PasswordBank.DataModel
{
    internal record CredentialInfo
    {
        public required string  Name         { get; set; }
        public required string? EmailAddress { get; set; }
        public required string? Username     { get; set; }
        public required string  Password     { get; set; }

        public EncryptedCredential Encrypt(byte[] key)
        {
            byte[] nonce = Cryptography.Random.GenerateBytes(12);
            (byte[] ciphertext, byte[] tag) = AES256.Encrypt(Password, key, nonce);

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
    }

    internal record EncryptedCredential
    {
        public required string  Name              { get; set; }
        public required string? EmailAddress      { get; set; }
        public required string? Username          { get; set; }
        public required byte[]  EncryptionNonce   { get; set; }
        public required byte[]  EncryptedPassword { get; set; }
        public required byte[]  EncryptionTag     { get; set; }

        public CredentialInfo Decrypt(byte[] key)
        {
            return new CredentialInfo()
            {
                Name = Name,
                EmailAddress = EmailAddress,
                Username = Username,
                Password = AES256.Decrypt(EncryptedPassword, key, EncryptionNonce, EncryptionTag),
            };
        }
    }
}
