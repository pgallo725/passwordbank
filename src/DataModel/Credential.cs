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
            byte[] iv = Cryptography.Random.GenerateBytes(16);
            return new EncryptedCredential()
            {
                Name = Name,
                EmailAddress = EmailAddress,
                Username = Username,
                EncryptedPassword = AES256.Encrypt(Password, key, iv),
                EncryptionIV = iv
            };
        }
    }

    internal record EncryptedCredential
    {
        public required string  Name              { get; set; }
        public required string? EmailAddress      { get; set; }
        public required string? Username          { get; set; }
        public required byte[]  EncryptedPassword { get; set; }
        public required byte[]  EncryptionIV      { get; set; }

        public CredentialInfo Decrypt(byte[] key)
        {
            return new CredentialInfo()
            {
                Name = Name,
                EmailAddress = EmailAddress,
                Username = Username,
                Password = AES256.Decrypt(EncryptedPassword, key, EncryptionIV),
            };
        }
    }
}
