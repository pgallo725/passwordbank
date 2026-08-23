using Microsoft.Data.Sqlite;
using PasswordBank.Cryptography;

namespace PasswordBank.DataModel
{
    internal class Database : IDisposable
    {
        /// <summary>
        /// The (static) path to the credentials database file.
        /// </summary>
        public static readonly string DatabaseFilePath = Path.Combine
        (
            AppDomain.CurrentDomain.BaseDirectory,  // directory where the application's .exe is located
            "credentials.db"                        // fixed name of the credentials database file
        );


        private readonly SqliteConnection connection;

        Database(SqliteConnection connection)
        {
            this.connection = connection;
        }

        public void Dispose()
        {
            connection?.Dispose();
        }


        /// <summary>
        /// Create and initialize an encrypted credentials database at the default location.
        /// </summary>
        /// <param name="password">The password to use for encrypting the database.</param>
        /// <exception cref="ApplicationException"></exception>
        public static Database Create(string password)
        {
            if (File.Exists(DatabaseFilePath))
                throw new ApplicationException("Database file already exists.");

            ArgumentException.ThrowIfNullOrEmpty(password);

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabaseFilePath,
                Password = password
            };
            var database = new Database(new SqliteConnection(builder.ConnectionString));

            try
            {
                database.connection.Open();

                // Create the CREDENTIALS table
                var command = database.connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE "Credentials" (
                        "AccountName"   TEXT NOT NULL,
                        "EmailAddress"  TEXT,
                        "Username"  TEXT,
                        "Password"  BLOB NOT NULL,
                        "CryptoIV"  BLOB NOT NULL,
                        PRIMARY KEY("AccountName")
                    );
                    """;
                command.ExecuteNonQuery();

                // Create the SERVICEDATA table
                command = database.connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE "ServiceData" (
                        "Name"  TEXT NOT NULL,
                        "Value" BLOB NOT NULL,
                        PRIMARY KEY("Name")
                    );
                    """;
                command.ExecuteNonQuery();

                // Generate the master salts and the master password hash
                byte[] verificationSalt = Cryptography.Random.GenerateBytes(16);
                byte[] encryptionSalt = Cryptography.Random.GenerateBytes(16);
                byte[] masterPasswordHash = PBKDF2.GenerateKey(password, verificationSalt);
                byte[] databaseVersion = BitConverter.GetBytes(1);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(databaseVersion); // make sure byte sequence is little endian

                // Insert these values into the ServiceData table
                int added = 0;
                command = database.connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ServiceData
                    VALUES('DatabaseVersion', $value);
                    """;
                command.Parameters.AddWithValue("$value", databaseVersion);
                added += command.ExecuteNonQuery();

                command = database.connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ServiceData
                    VALUES('VerificationSalt', $value);
                    """;
                command.Parameters.AddWithValue("$value", verificationSalt);
                added += command.ExecuteNonQuery();

                command = database.connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ServiceData
                    VALUES('EncryptionSalt', $value);
                    """;
                command.Parameters.AddWithValue("$value", encryptionSalt);
                added += command.ExecuteNonQuery();

                command = database.connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ServiceData
                    VALUES('MasterPasswordHash', $value);
                    """;
                command.Parameters.AddWithValue("$value", masterPasswordHash);
                added += command.ExecuteNonQuery();

                if (added != 4)
                    throw new ApplicationException("Failed to initialize SQL tables in the database.");

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
        }


        /// <summary>
        /// Open a connection to an existing (encrypted) credentials database.
        /// </summary>
        /// <param name="password">The password used to encrypt the database.</param>
        /// <returns>A reference to the opened database if the password was correct, otherwise null.</returns>
        /// <exception cref="ApplicationException"></exception>
        public static Database? Open(string password)
        {
            if (!File.Exists(DatabaseFilePath))
                throw new ApplicationException("Database file not found.");

            ArgumentException.ThrowIfNullOrEmpty(password);

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabaseFilePath,
                Password = password
            };
            var database = new Database(new SqliteConnection(builder.ConnectionString));

            try
            {
                // See note in the 'catch' block below
                database.connection.Open();

                // Validate the database schema by checking the presence of the required tables
                var command = database.connection.CreateCommand();
                command.CommandText = """
                    SELECT COUNT(*)
                    FROM sqlite_schema
                    WHERE type='table' AND name IN ('Credentials', 'ServiceData');
                    """;
                long found = (long)(command.ExecuteScalar() ?? 0);  // should return 2

                // Validate the schema of these tables
                if (found == 2)
                {
                    command = database.connection.CreateCommand();
                    command.CommandText = """
                        SELECT COUNT(*)
                        FROM pragma_table_info('Credentials')
                        WHERE (name='AccountName' AND type='TEXT' AND pk=1)
                           OR (name='EmailAddress' AND type='TEXT')
                           OR (name='Username' AND type='TEXT')
                           OR (name='Password' AND type='BLOB')
                           OR (name='CryptoIV' AND type='BLOB');
                        """;
                    found += (long)(command.ExecuteScalar() ?? 0);  // should return 5

                    command = database.connection.CreateCommand();
                    command.CommandText = """
                        SELECT COUNT(*)
                        FROM pragma_table_info('ServiceData')
                        WHERE (name='Name' AND type='TEXT' AND pk=1)
                           OR (name='Value' AND type='BLOB');
                        """;
                    found += (long)(command.ExecuteScalar() ?? 0);  // should return 2
                }

                // Validate the content of the ServiceData table
                if (found == 9)
                {
                    command = database.connection.CreateCommand();
                    command.CommandText = """
                        SELECT COUNT(*)
                        FROM ServiceData
                        WHERE Name IN ('DatabaseVersion', 'VerificationSalt', 'EncryptionSalt', 'MasterPasswordHash');
                        """;
                    found += (long)(command.ExecuteScalar() ?? 0);  // should return 4
                }

                // Validate the database version number
                int version = 0;
                if (found == 13)
                {
                    command = database.connection.CreateCommand();
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

                if (found != 13 || version != 1)
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
        /// Retrieve the encryption key generation salt from the database.
        /// </summary>
        /// <returns>The byte array containing the salt to use for generating the AES-256 encryption key.</returns>
        public byte[] GetEncryptionSalt()
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
        /// <returns>The byte array containing the salt to use for generating the PBKDF2 password hash for verification.</returns>
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
        /// <returns>A boolean value indicating whether the password passed the verification or not.</returns>
        public bool VerifyPassword(string password)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Name, Value
                FROM ServiceData
                WHERE Name = 'MasterPasswordHash' OR Name = 'VerificationSalt';
                """;
            using var reader = command.ExecuteReader();

            byte[] masterPasswordHash = [];
            byte[] verificationSalt = [];

            while (reader.Read())
            {
                switch (reader.GetString(0))
                {
                    case "MasterPasswordHash":
                        masterPasswordHash = reader.GetFieldValue<byte[]>(1);
                        break;

                    case "VerificationSalt":
                        verificationSalt = reader.GetFieldValue<byte[]>(1);
                        break;

                    default:
                        throw new ApplicationException(
                            "Unexpected value in column 'Name' of table 'ServiceData'."
                        );
                }
            }
            if (masterPasswordHash.Length == 0 || verificationSalt.Length == 0)
                throw new ApplicationException("Missing fields in database 'ServiceData' table.");

            byte[] providedPasswordHash = PBKDF2.GenerateKey(password, verificationSalt);
            return masterPasswordHash.SequenceEqual(providedPasswordHash);
        }


        /// <summary>
        /// Query the database for the total number of stored credentials.
        /// </summary>
        /// <returns>The number of credentials in the database.</returns>
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
        /// <returns>The list of credentials in the database.</returns>
        public List<EncryptedCredential> GetCredentials()
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT AccountName, EmailAddress, Username, Password, CryptoIV
                FROM Credentials;
                """;
            using var reader = command.ExecuteReader();

            var entries = new List<EncryptedCredential>();
            while (reader.Read())
            {
                string accountName       = reader.GetString(0);
                string? emailAddress     = reader.IsDBNull(1) ? null : reader.GetString(1);
                string? username         = reader.IsDBNull(2) ? null : reader.GetString(2);
                byte[] encryptedPassword = reader.GetFieldValue<byte[]>(3);
                byte[] encryptionIV      = reader.GetFieldValue<byte[]>(4);

                // Create entry with the values read from the DB
                EncryptedCredential credential = new()
                {
                    Name = accountName,
                    EmailAddress = emailAddress,
                    Username = username,
                    EncryptedPassword = encryptedPassword,
                    EncryptionIV = encryptionIV
                };
                entries.Add(credential);
            }
            return entries;
        }


        /// <summary>
        /// Search among the database for credentials whose account name or email address matches<br/>
        /// with the provided search string (partial matches supported on the account name field).
        /// </summary>
        /// <param name="searchString">The string used for searching credential records.</param>
        /// <param name="maxResults">The maximum number of possible matches returned by this function.</param>
        /// <returns>A limited list of records matching the search string.</returns>
        public List<EncryptedCredential> Search(string searchString, int maxResults = 100)
        {
            ArgumentException.ThrowIfNullOrEmpty(searchString);
            ArgumentOutOfRangeException.ThrowIfNegative(maxResults);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT AccountName, EmailAddress, Username, Password, CryptoIV
                FROM Credentials;
                """;
            using var reader = command.ExecuteReader();

            var entries = new List<EncryptedCredential>(maxResults);
            while (entries.Count < maxResults && reader.Read())
            {
                string accountName = reader.GetString(0);
                string? emailAddress = reader.IsDBNull(1) ? null : reader.GetString(1);
                string? username = reader.IsDBNull(2) ? null : reader.GetString(2);

                // TODO: employ a Levenshtein distance based search for more accurate results
                if (accountName.Contains(searchString, StringComparison.InvariantCultureIgnoreCase) ||
                    (emailAddress?.Equals(searchString, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    byte[] encryptedPassword = reader.GetFieldValue<byte[]>(3);
                    byte[] encryptionIV = reader.GetFieldValue<byte[]>(4);

                    // Create entry with the values read from the DB
                    EncryptedCredential credential = new()
                    {
                        Name = accountName,
                        EmailAddress = emailAddress,
                        Username = username,
                        EncryptedPassword = encryptedPassword,
                        EncryptionIV = encryptionIV
                    };
                    entries.Add(credential);
                }
            }
            return entries;
        }


        /// <summary>
        /// Query the database to check if it contains any credentials with the given account name.
        /// </summary>
        /// <param name="accountName">The account name to look for.</param>
        /// <returns>A boolean value indicating whether the database contains an account with the given name.</returns>
        public bool Contains(string accountName)
        {
            ArgumentException.ThrowIfNullOrEmpty(accountName);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM Credentials
                WHERE AccountName = $accountName;
                """;
            command.Parameters.AddWithValue("$accountName", accountName);
            long count = (long)(command.ExecuteScalar() ?? 0);
            return (count > 0);
        }


        /// <summary>
        /// Insert a new credential into the database.
        /// </summary>
        /// <param name="record">The credential record to insert.</param>
        /// <returns>A boolean value indicating whether the insertion succeeded or not.</returns>
        public bool Insert(EncryptedCredential record)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Credentials
                VALUES( $account, $email, $username, $password, $iv );
                """;
            command.Parameters.AddWithValue("$account", record.Name);
            command.Parameters.AddWithValue("$email", record.EmailAddress ?? string.Empty);
            command.Parameters.AddWithValue("$username", record.Username ?? string.Empty);
            command.Parameters.AddWithValue("$password", record.EncryptedPassword);
            command.Parameters.AddWithValue("$iv", record.EncryptionIV);
            int added = command.ExecuteNonQuery();
            return (added == 1);
        }


        /// <summary>
        /// Update an existing credential record in the database.
        /// </summary>
        /// <param name="oldRecord">The old credential record to update.</param>
        /// <param name="newRecord">The updated credential record.</param>
        /// <returns>A boolean value indicating whether the update succeeded or not.</returns>
        public bool Update(EncryptedCredential oldRecord, EncryptedCredential newRecord)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Credentials
                SET AccountName = $newAccount, EmailAddress = $email, Username = $username, Password = $password, CryptoIV = $iv
                WHERE AccountName = $oldAccount;
                """;
            command.Parameters.AddWithValue("$newAccount", newRecord.Name);
            command.Parameters.AddWithValue("$email", newRecord.EmailAddress ?? string.Empty);
            command.Parameters.AddWithValue("$username", newRecord.Username ?? string.Empty);
            command.Parameters.AddWithValue("$password", newRecord.EncryptedPassword);
            command.Parameters.AddWithValue("$iv", newRecord.EncryptionIV);
            command.Parameters.AddWithValue("$oldAccount", oldRecord.Name);

            int updated = command.ExecuteNonQuery();
            return (updated == 1);
        }


        /// <summary>
        /// Remove a credential record from the database.
        /// </summary>
        /// <param name="record">The credential record to be removed.</param>
        /// <returns>A boolean value indicating whether the removal succeeded or not.</returns>
        public bool Remove(EncryptedCredential record)
        {
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
        /// <returns>A boolean value indicating whether the operation succeeded or not.</returns>
        public bool ChangePassword(string oldPassword, string newPassword)
        {
            ArgumentException.ThrowIfNullOrEmpty(oldPassword);
            ArgumentException.ThrowIfNullOrEmpty(newPassword);

            List<EncryptedCredential> credentials = GetCredentials();

            // NOTE: the whole set of database updates required for a password change
            // is wrapped into a single transaction, to rollback in case anything fails.
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.CommandText = """
                    UPDATE Credentials
                    SET Password = $password
                    WHERE AccountName = $account;
                    """;
            command.Parameters.Add("$password", SqliteType.Blob);
            command.Parameters.Add("$account", SqliteType.Text);
            command.Prepare();

            byte[] encryptionSalt = GetEncryptionSalt();
            byte[] verificationSalt = GetVerificationSalt();
            byte[] oldEncryptionKey = PBKDF2.GenerateKey(oldPassword, encryptionSalt);
            byte[] newEncryptionKey = PBKDF2.GenerateKey(newPassword, encryptionSalt);
            byte[] newPasswordHash = PBKDF2.GenerateKey(newPassword, verificationSalt);

            try
            {
                // Loop over all credentials in the database and re-encrypt them with the new key
                int updated = 0;
                foreach (EncryptedCredential credential in credentials)
                {
                    string password = AES256.Decrypt(credential.EncryptedPassword, oldEncryptionKey, credential.EncryptionIV);
                    byte[] encryptedPassword = AES256.Encrypt(password, newEncryptionKey, credential.EncryptionIV);

                    command.Parameters[0].Value = encryptedPassword;
                    command.Parameters[1].Value = credential.Name;
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
                    // NOTE: SQLite doesn't support parameters in PRAGMA statements.
                    //  Instead, we use the quote() function to prevent SQL injection.
                    using var pragma = connection.CreateCommand();
                    pragma.CommandText = "SELECT quote($newPassword);";
                    pragma.Parameters.AddWithValue("$newPassword", newPassword);
                    if (pragma.ExecuteScalar() is string quotedNewPassword)
                    {
                        pragma.CommandText = $"PRAGMA rekey = {quotedNewPassword}";
                        pragma.Parameters.Clear();
                        pragma.ExecuteNonQuery();
                    }
                    else
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
                transaction.Commit();
                return true;
            }
            catch (Exception)
            {
                transaction.Rollback();
                return false;
            }
        }
    }
}
