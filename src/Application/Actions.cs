using PasswordBank.Application;
using PasswordBank.Cryptography;
using PasswordBank.DataModel;
using PasswordBank.Foundation;
using PasswordBank.UI;
using Spectre.Console;

namespace PasswordBank;

internal partial class Program
{
    /// <summary>
    /// The possible actions to perform from the PasswordBank main menu.
    /// </summary>
    internal enum MainMenuAction
    {
        View,
        Search,
        Add,
        ChangePassword,
        Quit
    };
    static string MainMenuActionTextConverter(MainMenuAction action)
    {
        return action switch
        {
            MainMenuAction.View           => $"[{ColorPalette.PRIMARY}]View[/] the list of all your credentials",
            MainMenuAction.Search         => $"[{ColorPalette.PRIMARY}]Search[/] for specific records in the database",
            MainMenuAction.Add            => $"[{ColorPalette.PRIMARY}]Add[/] new credentials to the encrypted database",
            MainMenuAction.ChangePassword => $"[{ColorPalette.PRIMARY}]Change[/] your master password [{ColorPalette.NEUTRAL}](recommended periodically)[/]",
            MainMenuAction.Quit           => $"[{ColorPalette.PRIMARY}]Quit[/] the application",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    /// <summary>
    /// The possible actions to perform on a selected credential record.
    /// </summary>
    internal enum CredentialAction
    {
        View,
        Edit,
        Delete,
        Cancel
    };
    static string CredentialActionTextConverter(CredentialAction action)
    {
        return action switch
        {
            CredentialAction.View   => $"[{ColorPalette.PRIMARY}]View[/] credential details",
            CredentialAction.Edit   => $"[{ColorPalette.PRIMARY}]Edit[/] the credential information",
            CredentialAction.Delete => $"[{ColorPalette.PRIMARY}]Delete[/] this record from the database",
            CredentialAction.Cancel => $"[{ColorPalette.PRIMARY}]Back[/] to the previous menu",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }


    /// <summary>
    /// [ACTION] Implements the procedure for creating a credentials database from scratch,<br/>
    /// triggered when the application doesn't detect a database at startup.<br/>
    /// Performs the following steps:<br/>
    ///     1. Prompts the user to select a master password (twice for confirmation)<br/>
    ///     1b. If the provided passwords do not match, go back to 1.
    ///     2. Executes the Database.Create() routine, reporting errors or success
    ///     3. Waits for user confirmation before returning
    /// </summary>
    /// <returns>A boolean value indicating whether the action was successful or not.</returns>
    internal static bool CreateDatabase()
    {
        // Introduction
        AnsiConsole.MarkupLine($"""
            Welcome to [{ColorPalette.PRIMARY}]PasswordBank[/]!

            Your credentials will be stored in a strongly encrypted database,
            accessible exclusively via your secret [{ColorPalette.SECONDARY}]master password[/].

            """);

        // Repeat the following block until the user provides matching passwords
        ConsoleHelper.SaveCheckpoint();
        while (true)
        {
            using SecretString masterPassword = SecretPrompt<MasterPasswordValidator>(
                $"Choose your [{ColorPalette.SECONDARY}]master password[/]:"
                );

            using SecretString confirmPassword = SecretPrompt<BasicValidator>(
                $"Confirm your [{ColorPalette.SECONDARY}]master password[/]:"
                );

            if (masterPassword.ContentEquals(confirmPassword))
            {
                bool result;
                try
                {
                    // NOTE: do not store the database object because we don't want to keep
                    //  an open connection while we're on the application main menu (unauthenticated)
                    Database.Create(masterPassword).Dispose();

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[{ColorPalette.SUCCESS}]Successfully created credentials database![/]");
                    result = true;
                }
                catch (Exception e)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]Failed to create credentials database![/]");
                    result = false;

                #if DEBUG   // in Debug mode, print the whole exception trace
                #pragma warning disable IL3050  // WriteException() only used in Debug, no need for AOT compilation
                    AnsiConsole.WriteException(e, Styles.EXCEPTION);
                #pragma warning restore IL3050
                #elif RELEASE   // in Release mode, print only a short summary of the error
                    AnsiConsole.MarkupLineInterpolated($"[{ColorPalette.ERROR}][[ERROR]] {e.Message}[/]");
                #endif
                }

                // Remove secrets from memory immediately
                masterPassword.Zeroise();
                confirmPassword.Zeroise();

                ContinuationPrompt();
                ConsoleHelper.DeleteCheckpoint();
                return result;
            }
            else
            {
                // Remove secrets from memory immediately
                masterPassword.Zeroise();
                confirmPassword.Zeroise();

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]The provided passwords do not match, please retry.[/]");

                ContinuationPrompt();
                ConsoleHelper.RestoreCheckpoint();
            }
        }
    }


    /// <summary>
    /// [ACTION] Implements the procedure for changing the master password of the database.<br/>
    /// Performs the following steps:<br/>
    ///     1. Authenticates the user and connects to the database<br/>
    ///     2. Prompts the user for the new master password (twice for confirmation)<br/>
    ///     3. Executes the ChangePassword() routine on the database, reporting errors or success<br/>
    ///     3b. While ChangePassword() executes, plays a spinner animation in the console
    /// </summary>
    internal static void ChangeMasterPassword()
    {
        // Action title
        AnsiConsole.MarkupLine($"""
            [{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]CHANGE MASTER PASSWORD[/]
            """);

        using Database? database = AuthenticationPrompt(out SecretString inputPassword);
        using SecretString currentPassword = inputPassword;

        if (database is null)
            return;

        using SecretString newPassword = SecretPrompt<MasterPasswordValidator>(
            $"Choose your new [{ColorPalette.SECONDARY}]master password[/]:"
            );

        using SecretString confirmPassword = SecretPrompt<BasicValidator>(
            $"Confirm your new [{ColorPalette.SECONDARY}]master password[/]:"
            );

        if (newPassword.ContentEquals(confirmPassword))
        {
            bool updateSuccessful = false;
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Ascii)
                .SpinnerStyle(new Style(ColorPalette.NEUTRAL_LIGHT))
                .Start($"[{ColorPalette.NEUTRAL_LIGHT}]Re-encrypting database with new master password...[/]", ctx =>
                {
                    // Update the master password in the database (re-encrypt all data)
                    updateSuccessful = database.ChangePassword(currentPassword, newPassword);
                });

            if (updateSuccessful)
                AnsiConsole.MarkupLine($"\n[{ColorPalette.SUCCESS}]Successfully updated master password![/]");
            else AnsiConsole.MarkupLine($"\n[{ColorPalette.ERROR}]Failed to update the master password![/]");
        }
        else
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]The provided passwords do not match, aborting.[/] ");
        }
    }


    /// <summary>
    /// [ACTION] Implements the procedure for retrieving the credentials stored in the database.<br/>
    /// Performs the following steps:<br/>
    ///     1. Authenticates the user and connects to the database<br/>
    ///     2. Loads the list of all credentials from the database (using the GetCredentials() API)
    /// </summary>
    /// <returns>The list of credentials retrieved from the database.</returns>
    internal static List<EncryptedCredential>? ListCredentials()
    {
        // Action title
        AnsiConsole.MarkupLine($"""
            [{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]LIST CREDENTIAL RECORDS[/]
            """);

        using Database? database = AuthenticationPrompt(out SecretString inputPassword);
        inputPassword.Zeroise();  // not used by this method

        if (database is null)
            return null;

        // Retrieve the list of credentials from the database
        return database.GetCredentials();
    }


    /// <summary>
    /// [ACTION] Implements the procedure for searching credentials in the database matching with user input.<br/>
    /// Performs the following steps:<br/>
    ///     1. Authenticates the user and connects to the database<br/>
    ///     2. Prompts the user to input the search string<br/>
    ///     3. Executes the Search() procedure on the database, retrieving the list of all matches
    /// </summary>
    /// <returns>The list of credentials from the database that match the user's search criteria.</returns>
    internal static List<EncryptedCredential>? SearchCredentials()
    {
        // Action title
        AnsiConsole.MarkupLine($"""
            [{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]SEARCH CREDENTIAL RECORDS[/]
            """);

        using Database? database = AuthenticationPrompt(out SecretString inputPassword);
        inputPassword.Zeroise();  // not used by this method

        if (database is null)
            return null;

        AnsiConsole.Markup($"[{ColorPalette.NEUTRAL}]Tip: [/]");
        AnsiConsole.Markup($"[{ColorPalette.NEUTRAL_LIGHT}]Find credentials whose identifier contains the provided search string.[/]\n");
        AnsiConsole.WriteLine();

        // Get the search string from user input
        string searchString = PlaintextPrompt<BasicValidator>(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}]Search[/] for:")
                .ClearOnFinish(true)
            );

        // Retrieve the list of credentials from the database
        return database.Search(searchString);
    }


    /// <summary>
    /// [ACTION] Implements the procedure for displaying the information of a credential record.<br/>
    /// Performs the following steps:<br/>
    ///     1. Authenticates the user and connects to the database<br/>
    ///     2. Generates decryption key from the master password and the salt in the database<br/>
    ///     3. Decrypts the credential record that needs to be displayed<br/>
    ///     4. Prints the credential information (plaintext) nicely formatted in a Spectre.Console table
    /// </summary>
    /// <param name="credential">The encrypted credential record that must be displayed to the user.</param>
    internal static void ViewCredential(EncryptedCredential credential)
    {
        // Action title
        AnsiConsole.MarkupLine($"""
            [{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]VIEW CREDENTIAL RECORD[/]
            """);

        using Database? database = AuthenticationPrompt(out SecretString inputPassword);
        using SecretString masterPassword = inputPassword;

        if (database is null)
            return;

        // Decrypt the credential record in-memory
        KeyGenerator keygen = database.GetKeyGenerator();
        using PlaintextCredential plaintextCredential = credential.Decrypt(keygen, masterPassword);

        // Display credential information in a table
        Table table = new();
        table.Title($"[{ColorPalette.PRIMARY} bold]Credential Information[/]");
        table.Border(TableBorder.Heavy);
        table.AddColumn("Key", c => c.Alignment(Justify.Right));
        table.AddColumn("Value", c => c.Alignment(Justify.Center));
        table.AddRow($"[{ColorPalette.SECONDARY}]Identifier[/]", Markup.Escape(plaintextCredential.Name));
        table.AddRow($"[{ColorPalette.SECONDARY}]Email Address[/]", Markup.Escape(plaintextCredential.EmailAddress ?? string.Empty));
        table.AddRow($"[{ColorPalette.SECONDARY}]Username[/]", Markup.Escape(plaintextCredential.Username ?? string.Empty));
        table.AddRow($"[{ColorPalette.SECONDARY}]Password[/]", Markup.Escape(plaintextCredential.Password.ToString())); // TODO: avoid the string conversion (?)
        table.HideHeaders();
        table.ShowRowSeparators();

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }


    /// <summary>
    /// [ACTION] Implements the procedure for adding a new credential record to the database.<br/>
    /// Performs the following steps:<br/>
    ///     1. Authenticates the user and connects to the database<br/>
    ///     2. Prompts the user to provide the credential information (Name, EmailAddress, Username, Password)<br/>
    ///     3. Generates encryption key from the master password and the salt in the database<br/>
    ///     4. Encrypts the credential record that needs to be stored in the database<br/>
    ///     5. Executes the Insert() procedure on the database, reporting errors or success<br/>
    /// </summary>
    internal static void AddCredential()
    {
        // Action title
        AnsiConsole.MarkupLine($"""
            [{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]ADD CREDENTIAL RECORD[/]
            """);

        using Database? database = AuthenticationPrompt(out SecretString inputPassword);
        using SecretString masterPassword = inputPassword;

        if (database is null)
            return;

        AnsiConsole.WriteLine("Please insert the requested credential information:");
        AnsiConsole.WriteLine();

        using PlaintextCredential plaintextCredential = new()
        {
            Name = PlaintextPrompt<IdentifierValidator>(
                new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Identifier:[/]")
                    .ClearOnFinish(false),
                new ValidationContext(database)
            ),
            EmailAddress = PlaintextPrompt<EmailAddressValidator>(
                new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Email Address:[/] [{ColorPalette.NEUTRAL}](optional)[/]")
                    .AllowEmpty()
                    .ClearOnFinish(false)
            ),
            Username = PlaintextPrompt<BasicValidator>(
                new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Username:[/] [{ColorPalette.NEUTRAL}](optional)[/]")
                    .AllowEmpty()
                    .ClearOnFinish(false)
            ),
            Password = SecretPrompt<PasswordValidator>($"[{ColorPalette.SECONDARY}] - Password:[/]")
        };
        // Encrypt the account information before adding it to the database
        KeyGenerator keygen = database.GetKeyGenerator();
        EncryptedCredential encryptedCredential = plaintextCredential.Encrypt(keygen, masterPassword);

        // Remove secrets from memory immediately
        plaintextCredential.Password.Zeroise();

        if (database.Insert(masterPassword, encryptedCredential))
            AnsiConsole.MarkupLine($"\n[{ColorPalette.SUCCESS}]Successfully added credential record to the database![/]");
        else AnsiConsole.MarkupLine($"\n[{ColorPalette.ERROR}]Failed to add the credential record to the database![/]");
    }


    /// <summary>
    /// [ACTION] Implements the procedure for editing the information of a credential record in the database.<br/>
    /// Performs the following steps:<br/>
    ///     1. Authenticates the user and connects to the database<br/>
    ///     2. Generates decryption key from the master password and the salt in the database<br/>
    ///     3. Decrypts the credential record that needs to be edited<br/>
    ///     4. Prompts the user to update the credential information (Name, EmailAddress, Username, Password)<br/>
    ///     5. Encrypts the updated credential record that needs to be stored in the database<br/>
    ///     6. Executes the Update() procedure on the database, reporting errors or success<br/>
    ///     7. If successful, it also updates the content of the credential record passed as input argument<br/>
    /// </summary>
    /// <param name="credential">The credential record that will be edited by the user.</param>
    internal static void EditCredential(EncryptedCredential credential)
    {
        // Action title
        AnsiConsole.MarkupLine($"""
            [{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]EDIT CREDENTIAL RECORD[/]
            """);

        using Database? database = AuthenticationPrompt(out SecretString inputPassword);
        using SecretString masterPassword = inputPassword;

        if (database is null)
            return;

        AnsiConsole.WriteLine("Please provide the updated credential information:");
        AnsiConsole.WriteLine();

        // Decrypt the previous credential information to use as default values
        KeyGenerator keygen = database.GetKeyGenerator();
        using PlaintextCredential plaintextCredential = credential.Decrypt(keygen, masterPassword);

        plaintextCredential.Name = PlaintextPrompt<IdentifierValidator>(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Identifier:[/]")
                .DefaultValue(plaintextCredential.Name)
                .EditableDefaultValue(true)
                .HideDefaultValue()
                .ClearOnFinish(false),
            new ValidationContext(database, plaintextCredential.Name)
            );

        plaintextCredential.EmailAddress = PlaintextPrompt<EmailAddressValidator>(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Email Address:[/]")
                .DefaultValue(plaintextCredential.EmailAddress ?? string.Empty)
                .EditableDefaultValue(true)
                .HideDefaultValue()
                .AllowEmpty()
                .ClearOnFinish(false)
            );

        plaintextCredential.Username = PlaintextPrompt<BasicValidator>(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Username:[/]")
                .DefaultValue(plaintextCredential.Username ?? string.Empty)
                .EditableDefaultValue(true)
                .HideDefaultValue()
                .AllowEmpty()
                .ClearOnFinish(false)
            );

        plaintextCredential.Password = SecretPrompt<PasswordValidator>(
            $"[{ColorPalette.SECONDARY}] - Password:[/]", plaintextCredential.Password
            );

        // Encrypt the account information before writing it to the database
        EncryptedCredential updatedCredential = plaintextCredential.Encrypt(keygen, masterPassword);

        // Remove secrets from memory immediately
        plaintextCredential.Password.Zeroise();

        // Update the database and the in-memory record as well
        if (database.Update(masterPassword, credential, updatedCredential) && credential.Set(updatedCredential))
            AnsiConsole.MarkupLine($"\n[{ColorPalette.SUCCESS}]Successfully updated credential record![/]");
        else AnsiConsole.MarkupLine($"\n[{ColorPalette.ERROR}]Failed to update the credential record![/]");
    }


    /// <summary>
    /// [ACTION] Implements the procedure for deleting a credential record from the database.<br/>
    /// Performs the following steps:<br/>
    ///     1. Authenticates the user and connects to the database<br/>
    ///     2. Executes the Remove() procedure on the database, reporting errors or success<br/>
    /// </summary>
    /// <param name="credential">The credential record that must be removed from the database.</param>
    /// <param name="credentials">A list of credentials from which the entry must be removed, if deletion is successful.</param>
    internal static void DeleteCredential(EncryptedCredential credential, List<EncryptedCredential>? credentials)
    {
        // Action title
        AnsiConsole.MarkupLine($"""
            [{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]DELETE CREDENTIAL RECORD[/]
            """);

        if (credentials != null && !credentials.Contains(credential))
            throw new ArgumentException(
                "The provided credentials list must contain the credential record to delete.",
                nameof(credentials));

        using Database? database = AuthenticationPrompt(out SecretString inputPassword);
        using SecretString masterPassword = inputPassword;

        if (database is null)
            return;

        // Remove the credential both the database and from the in-memory list
        if (database.Remove(masterPassword, credential) && (credentials?.Remove(credential) ?? true))
            AnsiConsole.MarkupLine($"[{ColorPalette.SUCCESS}]Successfully removed credential record from the database![/]");
        else AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]Failed to remove the credential record from the database[/]");
    }
}

