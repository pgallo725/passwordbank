using Microsoft.Data.Sqlite;
using PasswordBank.Cryptography;
using PasswordBank.Foundation;
using System.Security.Cryptography;
using System.Text;
using static SQLitePCL.raw;

namespace PasswordBank.DataModel
{
    internal sealed class Database : IDisposable
    {
        /// <summary>
        /// The (static) path to the credentials database file.
        /// </summary>
        public static readonly string DatabaseFilePath = Path.Combine
        (
            AppDomain.CurrentDomain.BaseDirectory,  // directory where the application's .exe is located
            "credentials.db"                        // fixed name of the credentials database file
        );

        /// <summary>
        /// This text is appended to the 'Data Source' field in the connection string<br/>
        /// to select the cipher scheme used for encrypting the database connection.
        /// </summary>
        private static readonly string CipherString = "cipher=chacha20";   // use ChaCha20 encryption + Poly1305 HMAC


        private readonly SqliteConnection connection;

        Database(SqliteConnection connection)
        {
            this.connection = connection;
        }

        public void Dispose()
        {
            connection?.Dispose();
        }


        private static void ApplyDatabasePassword(SqliteConnection connection, SecretString password)
        {
            int nKeyBytes = Encoding.UTF8.GetByteCount(password.GetData());
            using PooledArray<byte> keyBytes = new(nKeyBytes);

            // SQLite3 Multiple Ciphers recommends UTF-8 encoding for passphrases
            Encoding.UTF8.GetBytes(password.GetData(), keyBytes.GetData_W());

            var db = connection.Handle;
            int code = sqlite3_key(db, keyBytes.GetData());
            if (code != SQLITE_OK)
                throw new SqliteException($"sqlite3_key() failed with error code {code}.",
                    code, sqlite3_extended_errcode(db));
        }


        /// <summary>
        /// Create and initialize an encrypted credentials database at the default location.
        /// </summary>
        /// <param name="password">The password to use for encrypting the database.</param>
        /// <exception cref="ApplicationException"></exception>
        /// <returns>
        /// A reference to the created database.
        /// </returns>
        public static Database Create(SecretString password)
        {
            if (File.Exists(DatabaseFilePath))
                throw new ApplicationException("Database file already exists.");

            if (password is null || password.GetLength() == 0)
                throw new ArgumentException("Value cannot be a null or empty SecretString.", nameof(password));

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = $"file:{DatabaseFilePath}?{CipherString}"
            };
            var database = new Database(new SqliteConnection(builder.ConnectionString));

            // NOTE: no point in using stackalloc or PooledArray<byte>, because SqliteParameterCollection.AddWithValue()
            //  has no overload taking a Span<T> and we have to call ToArray() which allocates a new byte[] anyways.
            byte[] verificationSalt = new byte[PBKDF2.SaltSize];
            byte[] encryptionSalt = new byte[PBKDF2.SaltSize];
            byte[] masterPasswordHash = new byte[PBKDF2.OutputSize];
            try
            {
                database.connection.Open();
                ApplyDatabasePassword(database.connection, password);

                // Wrap database initialization into a single transaction, to rollback if anything fails
                using var transaction = database.connection.BeginTransaction();

                // Create the CREDENTIALS table
                using var command = database.connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE "Credentials" (
                        "AccountName"   TEXT NOT NULL,
                        "EmailAddress"  TEXT,
                        "Username"  TEXT,
                        "Nonce"  BLOB NOT NULL,
                        "Password"  BLOB NOT NULL,
                        "AuthTag"  BLOB NOT NULL,
                        PRIMARY KEY("AccountName")
                    );
                    """;
                command.ExecuteNonQuery();

                // Create the SERVICEDATA table
                command.Parameters.Clear();
                command.CommandText = """
                    CREATE TABLE "ServiceData" (
                        "Name"  TEXT NOT NULL,
                        "Value" BLOB NOT NULL,
                        PRIMARY KEY("Name")
                    );
                    """;
                command.ExecuteNonQuery();

                // Generate the master salts and the master password hash
                Cryptography.Random.Fill(verificationSalt);
                Cryptography.Random.Fill(encryptionSalt);
                PBKDF2.GenerateKey(password.GetData(), verificationSalt, masterPasswordHash);

                // Database schema version number
                byte[] databaseVersion = BitConverter.GetBytes(1);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(databaseVersion); // make sure byte sequence is little endian

                // Insert these values into the ServiceData table
                int added = 0;
                command.Parameters.Clear();
                command.CommandText = """
                    INSERT INTO ServiceData
                    VALUES('DatabaseVersion', $value);
                    """;
                command.Parameters.AddWithValue("$value", databaseVersion);
                added += command.ExecuteNonQuery();

                command.Parameters.Clear();
                command.CommandText = """
                    INSERT INTO ServiceData
                    VALUES('VerificationSalt', $value);
                    """;
                command.Parameters.AddWithValue("$value", verificationSalt);
                added += command.ExecuteNonQuery();

                command.Parameters.Clear();
                command.CommandText = """
                    INSERT INTO ServiceData
                    VALUES('EncryptionSalt', $value);
                    """;
                command.Parameters.AddWithValue("$value", encryptionSalt);
                added += command.ExecuteNonQuery();

                command.Parameters.Clear();
                command.CommandText = """
                    INSERT INTO ServiceData
                    VALUES('MasterPasswordHash', $value);
                    """;
                command.Parameters.AddWithValue("$value", masterPasswordHash);
                added += command.ExecuteNonQuery();

                if (added != 4)
                {
                    transaction.Rollback();
                    throw new ApplicationException("Failed to initialize SQL tables in the database.");
                }

                transaction.Commit();
                return database;
            }
            catch (Exception)
            {
                database.Dispose();

                if (File.Exists(DatabaseFilePath))
                {
                    // Delete the database file that we failed to initialize
                    File.Delete(DatabaseFilePath);
                }
                throw;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(verificationSalt);
                CryptographicOperations.ZeroMemory(encryptionSalt);
                CryptographicOperations.ZeroMemory(masterPasswordHash);
            }
        }


        /// <summary>
        /// Open a connection to an existing (encrypted) credentials database.
        /// </summary>
        /// <param name="password">The password used to encrypt the database.</param>
        /// <exception cref="ApplicationException"></exception>
        /// <returns>
        /// A reference to the opened database if the password was correct, otherwise null.
        /// </returns>
        public static Database? Open(SecretString password)
        {
            if (!File.Exists(DatabaseFilePath))
                throw new ApplicationException("Database file not found.");

            if (password is null || password.GetLength() == 0)
                throw new ArgumentException("Value cannot be a null or empty SecretString.", nameof(password));

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = $"file:{DatabaseFilePath}?{CipherString}"
            };
            var database = new Database(new SqliteConnection(builder.ConnectionString));

            try
            {
                // See note in the 'catch' block below
                database.connection.Open();
                ApplyDatabasePassword(database.connection, password);

                // Validate the database schema by checking the presence of the required tables
                using var command = database.connection.CreateCommand();
                command.CommandText = """
                    SELECT COUNT(*)
                    FROM sqlite_schema
                    WHERE type='table' AND name IN ('Credentials', 'ServiceData');
                    """;
                long found = (long)(command.ExecuteScalar() ?? 0);  // should return 2

                // Validate the schema of these tables
                if (found == 2)
                {
                    command.Parameters.Clear();
                    command.CommandText = """
                        SELECT COUNT(*)
                        FROM pragma_table_info('Credentials')
                        WHERE (name='AccountName' AND type='TEXT' AND pk=1)
                           OR (name='EmailAddress' AND type='TEXT')
                           OR (name='Username' AND type='TEXT')
                           OR (name='Nonce' AND type='BLOB')
                           OR (name='Password' AND type='BLOB')
                           OR (name='AuthTag' AND type='BLOB');
                        """;
                    found += (long)(command.ExecuteScalar() ?? 0);  // should return 6

                    command.Parameters.Clear();
                    command.CommandText = """
                        SELECT COUNT(*)
                        FROM pragma_table_info('ServiceData')
                        WHERE (name='Name' AND type='TEXT' AND pk=1)
                           OR (name='Value' AND type='BLOB');
                        """;
                    found += (long)(command.ExecuteScalar() ?? 0);  // should return 2
                }

                // Validate the content of the ServiceData table
                if (found == 10)
                {
                    command.Parameters.Clear();
                    command.CommandText = """
                        SELECT COUNT(*)
                        FROM ServiceData
                        WHERE Name IN ('DatabaseVersion', 'VerificationSalt', 'EncryptionSalt', 'MasterPasswordHash');
                        """;
                    found += (long)(command.ExecuteScalar() ?? 0);  // should return 4
                }

                // Validate the database version number
                int version = 0;
                if (found == 14)
                {
                    command.Parameters.Clear();
                    command.CommandText = """
                        SELECT Value
                        FROM ServiceData
                        WHERE Name='DatabaseVersion';
                        """;
                    using var reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        byte[] versionBytes = reader.GetFieldValue<byte[]>(0);
                        if (!BitConverter.IsLittleEndian)
                            Array.Reverse(versionBytes);
                        version = BitConverter.ToInt32(versionBytes);
                    }
                }

                if (found != 14 || version != 1)
                    throw new ApplicationException("Invalid database schema.");

                return database;
            }
            catch (Exception e)
            {
                database.Dispose();

                if (e is SqliteException sqle)
                {
                    // NOTE: when the provided password is wrong, SQLite responds with error 26: 'file is not a database'.
                    //  We don't have a proper way to distinguish the case of a wrong password from the other cases
                    //  when the same error is thrown, so we just consider them all as password errors and we return
                    //  gracefully instead of throwing an exception.
                    if (sqle.SqliteErrorCode == 26)
                        return null;
                }
                throw;
            }
        }


        /// <summary>
        /// Instantiate a KeyGenerator object that, provided with the master password,<br/>
        /// can be used to generate the AES-256 key used to encrypt records in the database.
        /// </summary>
        /// <returns>A new KeyGenerator instance.</returns>
        public KeyGenerator GetKeyGenerator()
        {
            return new KeyGenerator(GetEncryptionSalt());
        }


        /// <summary>
        /// Retrieve the encryption key generation salt from the database.
        /// </summary>
        /// <returns>
        /// The byte array containing the salt to use for generating the AES-256 encryption key.
        /// </returns>
        private byte[] GetEncryptionSalt()
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Value
                FROM ServiceData
                WHERE Name = 'EncryptionSalt';
                """;
            using var reader = command.ExecuteReader();

            reader.Read();
            return reader.GetFieldValue<byte[]>(0);
        }

        /// <summary>
        /// Retrieve the verification hash generation salt from the database.
        /// </summary>
        /// <returns>
        /// The byte array containing the salt to use for generating the PBKDF2 password hash for verification.
        /// </returns>
        private byte[] GetVerificationSalt()
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Value
                FROM ServiceData
                WHERE Name = 'VerificationSalt';
                """;
            using var reader = command.ExecuteReader();

            reader.Read();
            return reader.GetFieldValue<byte[]>(0);
        }


        /// <summary>
        /// Safely check a user provided password against the one stored in the database.
        /// </summary>
        /// <param name="password">The password to verify.</param>
        /// <returns>
        /// A boolean value indicating whether the password passed the verification or not.
        /// </returns>
        public bool VerifyPassword(SecretString password)
        {
            if (password is null || password.GetLength() == 0)
                throw new ArgumentException("Value cannot be a null or empty SecretString.", nameof(password));

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Name, Value
                FROM ServiceData
                WHERE Name = 'MasterPasswordHash' OR Name = 'VerificationSalt';
                """;
            using var reader = command.ExecuteReader();

            // Allocate buffers from a pool to store all the temporary data used by this function
            using PooledArray<byte> inputPasswordHash = new(PBKDF2.OutputSize);
            using PooledArray<byte> masterPasswordHash = new(PBKDF2.OutputSize);
            using PooledArray<byte> verificationSalt = new(PBKDF2.SaltSize);

            bool hasMasterHash = false;
            bool hasSalt = false;

            while (reader.Read())
            {
                switch (reader.GetString(0))
                {
                    case "MasterPasswordHash":
                        if (reader.GetBytes(1, 0, masterPasswordHash.Buffer, 0, PBKDF2.OutputSize) != PBKDF2.OutputSize)
                            throw new ApplicationException("Invalid master password hash retrieved from the database.");
                        hasMasterHash = true;
                        break;

                    case "VerificationSalt":
                        if (reader.GetBytes(1, 0, verificationSalt.Buffer, 0, PBKDF2.SaltSize) != PBKDF2.SaltSize)
                            throw new ApplicationException("Invalid password verification salt retrieved from the database.");
                        hasSalt = true;
                        break;

                    default:
                        throw new ApplicationException(
                            "Unexpected value in column 'Name' of table 'ServiceData'."
                        );
                }
            }

            if (!hasSalt || !hasMasterHash)
                throw new ApplicationException("Missing fields in database 'ServiceData' table.");

            PBKDF2.GenerateKey(password.GetData(), verificationSalt.GetData(), inputPasswordHash.GetData_W());
            return Hash.Equals(inputPasswordHash.GetData(), masterPasswordHash.GetData());
        }


        /// <summary>
        /// Query the database for the total number of stored credentials.
        /// </summary>
        /// <returns>
        /// The number of credentials in the database.
        /// </returns>
        public long GetNumberOfCredentials()
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM Credentials;
                """;
            return (long)(command.ExecuteScalar() ?? 0);
        }


        /// <summary>
        /// Query the database for a list of all stored credentials.
        /// </summary>
        /// <returns>
        /// The list of credentials in the database.
        /// </returns>
        public List<EncryptedCredential> GetCredentials()
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT AccountName, EmailAddress, Username, Nonce, Password, AuthTag
                FROM Credentials
                ORDER BY AccountName COLLATE NOCASE ASC;
                """;
            using var reader = command.ExecuteReader();

            var credentials = new List<EncryptedCredential>();
            while (reader.Read())
            {
                // Create credential record with the values read from the DB
                EncryptedCredential credential = new()
                {
                    Name              = reader.GetString(0),
                    EmailAddress      = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Username          = reader.IsDBNull(2) ? null : reader.GetString(2),
                    EncryptionNonce   = reader.GetFieldValue<byte[]>(3),
                    EncryptedPassword = reader.GetFieldValue<byte[]>(4),
                    EncryptionTag     = reader.GetFieldValue<byte[]>(5)
                };
                credentials.Add(credential);
            }
            return credentials;
        }


        /// <summary>
        /// Search the database for credentials whose identifier contains the provided search string.
        /// </summary>
        /// <param name="searchString">The string used for searching credential records.</param>
        /// <param name="maxResults">The maximum number of possible matches returned by this function.</param>
        /// <returns>
        /// A limited list of records matching the search string.
        /// </returns>
        public List<EncryptedCredential> Search(string searchString, int maxResults = 100)
        {
            ArgumentException.ThrowIfNullOrEmpty(searchString);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT AccountName, EmailAddress, Username, Nonce, Password, AuthTag
                FROM Credentials;
                """;
            using var reader = command.ExecuteReader();

            var entries = new List<EncryptedCredential>();
            while (entries.Count < maxResults && reader.Read())
            {
                string identifier = reader.GetString(0);

                // TODO: employ a Levenshtein distance based search for more accurate results
                if (identifier.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                {
                    // Create entry with the values read from the DB
                    EncryptedCredential credential = new()
                    {
                        Name              = identifier,
                        EmailAddress      = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Username          = reader.IsDBNull(2) ? null : reader.GetString(2),
                        EncryptionNonce   = reader.GetFieldValue<byte[]>(3),
                        EncryptedPassword = reader.GetFieldValue<byte[]>(4),
                        EncryptionTag     = reader.GetFieldValue<byte[]>(5)
                    };
                    entries.Add(credential);
                }
            }
            return entries;
        }


        /// <summary>
        /// Query the database to check if it contains any credentials with the given identifier.
        /// </summary>
        /// <param name="identifier">The credential name to look for.</param>
        /// <returns>A boolean value indicating whether the database contains an account with the given name.</returns>
        public bool Contains(string identifier)
        {
            ArgumentException.ThrowIfNullOrEmpty(identifier);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM Credentials
                WHERE AccountName = $accountName;
                """;
            command.Parameters.AddWithValue("$accountName", identifier);
            long count = (long)(command.ExecuteScalar() ?? 0);
            return (count > 0);
        }


        /// <summary>
        /// Insert a new credential into the database.
        /// </summary>
        /// <param name="password">The database password, to authorize the operation.</param>
        /// <param name="record">The credential record to insert.</param>
        /// <returns>
        /// A boolean value indicating whether the insertion succeeded or not.
        /// </returns>
        public bool Insert(SecretString password, EncryptedCredential record)
        {
            if (password is null || password.GetLength() == 0)
                throw new ArgumentException("Value cannot be a null or empty SecretString.", nameof(password));

            if (!VerifyPassword(password))
                return false;

            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Credentials
                VALUES( $account, $email, $username, $nonce, $password, $tag );
                """;
            command.Parameters.AddWithValue("$account", record.Name);
            command.Parameters.AddWithValue("$email", record.EmailAddress ?? string.Empty);
            command.Parameters.AddWithValue("$username", record.Username ?? string.Empty);
            command.Parameters.AddWithValue("$nonce", record.EncryptionNonce);
            command.Parameters.AddWithValue("$password", record.EncryptedPassword);
            command.Parameters.AddWithValue("$tag", record.EncryptionTag);
            int added = command.ExecuteNonQuery();
            return (added == 1);
        }


        /// <summary>
        /// Update an existing credential record in the database.
        /// </summary>
        /// <param name="password">The database password, to authorize the operation.</param>
        /// <param name="oldRecord">The old credential record to update.</param>
        /// <param name="newRecord">The updated credential record.</param>
        /// <returns>
        /// A boolean value indicating whether the update succeeded or not.
        /// </returns>
        public bool Update(SecretString password, EncryptedCredential oldRecord, EncryptedCredential newRecord)
        {
            if (password is null || password.GetLength() == 0)
                throw new ArgumentException("Value cannot be a null or empty SecretString.", nameof(password));

            if (!VerifyPassword(password))
                return false;

            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Credentials
                SET AccountName = $newAccount, EmailAddress = $email, Username = $username,
                    Nonce = $nonce, Password = $password, AuthTag = $tag
                WHERE AccountName = $oldAccount;
                """;
            command.Parameters.AddWithValue("$newAccount", newRecord.Name);
            command.Parameters.AddWithValue("$email", newRecord.EmailAddress ?? string.Empty);
            command.Parameters.AddWithValue("$username", newRecord.Username ?? string.Empty);
            command.Parameters.AddWithValue("$nonce", newRecord.EncryptionNonce);
            command.Parameters.AddWithValue("$password", newRecord.EncryptedPassword);
            command.Parameters.AddWithValue("$tag", newRecord.EncryptionTag);
            command.Parameters.AddWithValue("$oldAccount", oldRecord.Name);

            int updated = command.ExecuteNonQuery();
            return (updated == 1);
        }


        /// <summary>
        /// Remove a credential record from the database.
        /// </summary>
        /// <param name="password">The database password, to authorize the operation.</param>
        /// <param name="record">The credential record to be removed.</param>
        /// <returns>
        /// A boolean value indicating whether the removal succeeded or not.
        /// </returns>
        public bool Remove(SecretString password, EncryptedCredential record)
        {
            if (password is null || password.GetLength() == 0)
                throw new ArgumentException("Value cannot be a null or empty SecretString.", nameof(password));

            if (!VerifyPassword(password))
                return false;

            using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM Credentials
                WHERE AccountName = $account;
                """;
            command.Parameters.AddWithValue("$account", record.Name);

            int deleted = command.ExecuteNonQuery();
            return (deleted == 1);
        }


        /// <summary>
        /// Change the master password with which the database and all credential passwords are encrypted.
        /// </summary>
        /// <param name="oldPassword">The password previously used to encrypt the database.</param>
        /// <param name="newPassword">The new password to use.</param>
        /// <returns>
        /// A boolean value indicating whether the operation succeeded or not.
        /// </returns>
        public bool ChangePassword(SecretString oldPassword, SecretString newPassword)
        {
            if (oldPassword is null || oldPassword.GetLength() == 0)
                throw new ArgumentException("Value cannot be a null or empty SecretString.", nameof(oldPassword));
            if (newPassword is null || newPassword.GetLength() == 0)
                throw new ArgumentException("Value cannot be a null or empty SecretString.", nameof(newPassword));

            // Authenticate the user before modifying the database
            if (!VerifyPassword(oldPassword))
                return false;

            List<EncryptedCredential> credentials = GetCredentials();

            // NOTE: the whole set of database updates required for a password change
            // is wrapped into a single transaction, to rollback in case anything fails.
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.CommandText = """
                    UPDATE Credentials
                    SET Nonce = $nonce, Password = $password, AuthTag = $tag
                    WHERE AccountName = $account;
                    """;
            command.Parameters.Add("$nonce", SqliteType.Blob);
            command.Parameters.Add("$password", SqliteType.Blob);
            command.Parameters.Add("$tag", SqliteType.Blob);
            command.Parameters.Add("$account", SqliteType.Text);
            command.Prepare();

            KeyGenerator keygen = GetKeyGenerator();
            byte[] verificationSalt = GetVerificationSalt();
            byte[] newPasswordHash = new byte[PBKDF2.OutputSize];

            Span<byte> oldEncryptionKey = stackalloc byte[AES256.KeySize];
            Span<byte> newEncryptionKey = stackalloc byte[AES256.KeySize];
            try
            {
                // Generate the hash for verifying the new master password
                PBKDF2.GenerateKey(newPassword.GetData(), verificationSalt, newPasswordHash);

                // Generate the keys only once before the loop over all database records
                keygen.GetKey(oldPassword.GetData(), oldEncryptionKey);
                keygen.GetKey(newPassword.GetData(), newEncryptionKey);

                // Loop over all credentials in the database and re-encrypt them with the new key
                int updated = 0;
                foreach (EncryptedCredential oldEncryptedCredential in credentials)
                {
                    using PlaintextCredential plaintextCredential = oldEncryptedCredential.Decrypt(oldEncryptionKey);
                    EncryptedCredential newEncryptedCredential = plaintextCredential.Encrypt(newEncryptionKey);
                    plaintextCredential.Password.Zeroise(); // remove plaintext secrets from memory immediately

                    command.Parameters[0].Value = newEncryptedCredential.EncryptionNonce;
                    command.Parameters[1].Value = newEncryptedCredential.EncryptedPassword;
                    command.Parameters[2].Value = newEncryptedCredential.EncryptionTag;
                    command.Parameters[3].Value = oldEncryptedCredential.Name;
                    updated += command.ExecuteNonQuery();
                }

                // Update the master password hash in the ServiceData table
                command.Parameters.Clear();
                command.CommandText = """
                    UPDATE ServiceData
                    SET Value = $hash
                    WHERE Name = 'MasterPasswordHash';
                    """;
                command.Parameters.AddWithValue("$hash", newPasswordHash);
                updated += command.ExecuteNonQuery();

                if (updated != credentials.Count + 1 /* for the password hash */)
                {
                    transaction.Rollback();
                    return false;
                }
                else // finally, change the SQLite encryption key and password as well
                {
                    int nKeyBytes = Encoding.UTF8.GetByteCount(newPassword.GetData());
                    using PooledArray<byte> keyBytes = new(nKeyBytes);

                    // SQLite3 Multiple Ciphers recommends UTF-8 encoding for passphrases
                    Encoding.UTF8.GetBytes(newPassword.GetData(), keyBytes.GetData_W());

                    var db = connection.Handle;
                    int code = sqlite3_rekey(db, keyBytes.GetData());
                    if (code != SQLITE_OK)
                        throw new SqliteException($"sqlite3_rekey() failed with error code {code}.",
                            code, sqlite3_extended_errcode(db));
                }
                transaction.Commit();
                return true;
            }
            catch (Exception)
            {
                transaction.Rollback();
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(newPasswordHash);
                CryptographicOperations.ZeroMemory(oldEncryptionKey);
                CryptographicOperations.ZeroMemory(newEncryptionKey);
            }
        }
    }
}
