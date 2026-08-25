using PasswordBank.Application;
using PasswordBank.Cryptography;
using PasswordBank.DataModel;
using PasswordBank.UI;
using Spectre.Console;

namespace PasswordBank;

internal class Program
{
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
            MainMenuAction.View           => $"[{ColorPalette.PRIMARY}]View[/] the list of all stored credentials",
            MainMenuAction.Search         => $"[{ColorPalette.PRIMARY}]Search[/] for an account in your stored credentials",
            MainMenuAction.Add            => $"[{ColorPalette.PRIMARY}]Add[/] new account credentials to the database",
            MainMenuAction.ChangePassword => $"[{ColorPalette.PRIMARY}]Change[/] your master password",
            MainMenuAction.Quit           => $"[{ColorPalette.PRIMARY}]Exit[/] PasswordBank",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

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
            CredentialAction.View   => $"[{ColorPalette.PRIMARY}]View[/] the account credentials",
            CredentialAction.Edit   => $"[{ColorPalette.PRIMARY}]Edit[/] the account credentials",
            CredentialAction.Delete => $"[{ColorPalette.PRIMARY}]Delete[/] the account credentials from the database",
            CredentialAction.Cancel => $"[{ColorPalette.PRIMARY}]Back[/] to the list of credentials",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }


    /// <summary>
    /// An open connection to the credentials database, this is realized the first time <br/>
    ///  that it's required and then cached for the lifetime of the program.
    /// </summary>
    private static Database? database;

    /// <summary>
    /// Style used throughout the application for highlighting the currently selected option.
    /// </summary>
    private static readonly Style HighlightStyle = new ()
    {
        Foreground = ColorPalette.HIGHLIGHT,
        Decoration = Decoration.None
    };

    /// <summary>
    /// A 'null' EncryptedCredential object to use as cancellation result.
    /// </summary>
    private static readonly EncryptedCredential NullCredential = new()
    {
        Name = string.Empty,
        EmailAddress = null,
        Username = null,
        EncryptionNonce = [],
        EncryptedPassword = [],
        EncryptionTag = []
    };


    static void Main(string[] args)
    {
        // Application title
        Console.Title = "PasswordBank v1.0.0";
        AnsiConsole.Write(new Panel($"""
            [{ColorPalette.PRIMARY}]
                _____                                           _  ____                 _    
               |  __ \                                         | ||  _ \               | |   
               | |__) |__ _  ___  ___ __      __ ___   _ __  __| || |_) |  __ _  _ __  | | __
               |  ___// _` |/ __|/ __|\ \ /\ / // _ \ | '__|/ _` ||  _ <  / _` || '_ \ | |/ /
               | |   | (_| |\__ \\__ \ \ V  V /| (_) || |  | (_| || |_) || (_| || | | ||   < 
               |_|    \__,_||___/|___/  \_/\_/  \___/ |_|   \__,_||____/  \__,_||_| |_||_|\_\[/]
            [{ColorPalette.SECONDARY}]
            Version 1.0.0                                                    developed by JackaL[/]

            """)
            .RoundedBorder()
            .BorderColor(ColorPalette.NEUTRAL)
            .PadLeft(2)
            .PadRight(2)
        );
        AnsiConsole.Write('\n');


        // Trigger database creation workflow if 'credentials.db' file is not found
        if (!File.Exists(Database.DatabaseFilePath))
        {
            ConsoleHelper.SaveCheckpoint();
            {
                bool success = Action_CreateDatabase();
                if (!success)
                    return;
            }
            ConsoleHelper.RestoreCheckpoint();
            ConsoleHelper.DeleteCheckpoint();
        }

        ConsoleHelper.SaveCheckpoint();

        // Application loop
        while (true)
        {
            try
            {
                // Show the main menu
                MainMenuAction mainAction = AnsiConsole.Prompt(
                    new SelectionPrompt<MainMenuAction>()
                        .Title($"Select one of the available [{ColorPalette.PRIMARY}]operations[/]: ")
                        .UseConverter(MainMenuActionTextConverter)
                        .AddChoices(MainMenuAction.View,
                                    MainMenuAction.Search,
                                    MainMenuAction.Add,
                                    MainMenuAction.ChangePassword,
                                    MainMenuAction.Quit)
                        .AddCancelResult(MainMenuAction.Quit)
                        .HighlightStyle(HighlightStyle)
                );
                // Clear the main menu before continuing into the selected action
                ConsoleHelper.RestoreCheckpoint();

                switch (mainAction)
                {
                    case MainMenuAction.View:
                    case MainMenuAction.Search:
                    {
                        // Retrieve the list of relevant passwords from the database (after authentication)
                        List<EncryptedCredential>? credentials = mainAction switch
                        {
                            MainMenuAction.View => Action_ListCredentials(),
                            MainMenuAction.Search => Action_SearchCredentials(),
                            _ => null
                        };
                        ConsoleHelper.RestoreCheckpoint();

                        if (credentials is null)
                            break;

                        if (credentials.Count == 0)
                        {
                            AnsiConsole.WriteLine("No credentials found in the database.\n");
                            ContinuationPrompt();
                        }
                        else while (true)
                        {
                            // Display the list of credentials and let the user select one
                            EncryptedCredential credential = AnsiConsole.Prompt(
                                new SelectionPrompt<EncryptedCredential>()
                                    .Title("Select a credential record to access further options:")
                                    .UseConverter(c => c.Name)
                                    .AddChoices(credentials)
                                    .AddCancelResult(NullCredential)
                                    .PageSize(10)
                                    .MoreChoicesText($"[{ColorPalette.NEUTRAL}](Use arrow keys to see more credentials)[/]")
                                    .WrapAround(true)
                                    .HighlightStyle(HighlightStyle)
                            );
                            // Clear the credentials list before continuing to the next menu
                            ConsoleHelper.RestoreCheckpoint();

                            if (credential == NullCredential)
                                break;

                            // Show the menu to choose the action for the selected credential
                            CredentialAction credentialAction = AnsiConsole.Prompt(
                                new SelectionPrompt<CredentialAction>()
                                    .Title($"Select one of the available [{ColorPalette.PRIMARY}]operations[/] for the selected credential: ")
                                    .UseConverter(CredentialActionTextConverter)
                                    .AddChoices(CredentialAction.View,
                                                CredentialAction.Edit,
                                                CredentialAction.Delete,
                                                CredentialAction.Cancel)
                                    .AddCancelResult(CredentialAction.Cancel)
                                    .HighlightStyle(HighlightStyle)
                            );
                            // Clear the menu before continuing into the selected action
                            ConsoleHelper.RestoreCheckpoint();

                            switch (credentialAction)
                            {
                                case CredentialAction.View:   Action_ViewCredential(credential);                break;
                                case CredentialAction.Edit:   Action_EditCredential(credential);                break;
                                case CredentialAction.Delete: Action_DeleteCredential(credential, credentials); break;
                                case CredentialAction.Cancel: /* nothing to do, show the previous menu again */ break;
                            }
                            // Clear everything before showing the (updated) list of credentials again
                            ConsoleHelper.RestoreCheckpoint();
                        }
                    } break;

                    case MainMenuAction.Add:
                        Action_AddCredential();
                        break;

                    case MainMenuAction.ChangePassword:
                        Action_ChangeMasterPassword();
                        break;

                    case MainMenuAction.Quit:
                        // Nothing to do, just exit from the application loop
                        return;
                }
            }
            catch (Exception e)
            {
                // Print error message, wait for user confirmation and reset to main menu
                AnsiConsole.MarkupLineInterpolated($"\n[{ColorPalette.ERROR}][[ERROR]] {e.Message}[/]");
                ContinuationPrompt();
            }

            // Close any open database connection, because the user is
            //  unauthenticated while on the application's main menu
            database?.Dispose();
            database = null;

            // Clear everything before showing the main menu again
            ConsoleHelper.RestoreCheckpoint();
        }
    }





    private static string ValidatedTextPrompt(TextPrompt<string> prompt, IValidator validator)
    {
        // Store the row number before the prompt
        (int column, int row) = Console.GetCursorPosition();

        // Disable built-in prompt validation
        prompt.Validator = null;
        prompt.ValidationErrorMessage = string.Empty;

        // Setup prompt to show editable default values
        prompt.ShowDefaultValue = false;
        prompt.EditableDefaultValue = true;

        while (true)
        {
            string input = AnsiConsole.Prompt(prompt);
            if (string.IsNullOrEmpty(input) && prompt.AllowEmpty)
                return input;

            (bool isValid, string? errorMessage) = validator.Validate(input);
            if (!isValid)
            {
                // Print error message and set the current input as the default value
                // for the existing prompt, so that the user can edit in-place
                AnsiConsole.MarkupLineInterpolated($"[{ColorPalette.ERROR}]{errorMessage}[/]");
                prompt.DefaultValue(input);
                Console.SetCursorPosition(column, row); // the updated prompt will cover the existing text perfectly
            }
            else
            {
                Console.SetCursorPosition(column, row + 1);
                AnsiConsole.Write(new string(' ', Console.WindowWidth));  // clear error line
                Console.SetCursorPosition(column, row + 1);
                return input;
            }
        }
    }


    private static void ContinuationPrompt()
    {
        AnsiConsole.Markup($"[{ColorPalette.NEUTRAL}]Press[/] " +
            $"[{ColorPalette.NEUTRAL_LIGHT} slowblink]Enter[/] " +
            $"[{ColorPalette.NEUTRAL}]to continue...[/]");

        ConsoleHelper.WaitForEnterKey();
    }


    private static string AuthenticationPrompt()
    {
        // Action title
        AnsiConsole.Markup($"[{ColorPalette.NEUTRAL}]>> AUTHENTICATION[/]");

        ConsoleHelper.SaveCheckpoint();

        AnsiConsole.Write('\n');
        AnsiConsole.Write('\n');

        string password = AnsiConsole.Prompt(
            new TextPrompt<string>($"Insert your [{ColorPalette.SECONDARY} slowblink]master password[/]: ")
                .Secret()
                .ClearOnFinish(false)
            );

        // Open the database connection on the first use
        database ??= Database.Open(password);

        ConsoleHelper.RestoreCheckpoint();
        ConsoleHelper.DeleteCheckpoint();

        // Check the password against the key stored in the database
        if (database is null || !database.VerifyPassword(password))
        {
            AnsiConsole.MarkupLine($" [{ColorPalette.ERROR}]FAILED[/]");
            return string.Empty;
        }
        else
        {
            AnsiConsole.MarkupLine($" [{ColorPalette.PRIMARY}]SUCCESSFUL[/]");
            return password;
        }
    }


    private static bool Action_CreateDatabase()
    {
        AnsiConsole.MarkupLine($"Welcome to [{ColorPalette.PRIMARY}]PasswordBank[/]!");
        AnsiConsole.MarkupLine($"No [italic]credentials.db[/] file detected, [slowblink]initializing new database...[/]\n");

        // Repeat the following block until the user provides matching passwords
        ConsoleHelper.SaveCheckpoint();
        while (true)
        {
            string firstPassword = ValidatedTextPrompt(
                new TextPrompt<string>($"Choose your [{ColorPalette.SECONDARY}]master password[/]: ")
                    .Secret()
                    .ClearOnFinish(false),
                new MasterPasswordValidator()
                );

            string secondPassword = ValidatedTextPrompt(
                new TextPrompt<string>($"Confirm your [{ColorPalette.SECONDARY}]master password[/]: ")
                    .Secret()
                    .ClearOnFinish(false),
                new BasicValidator()
                );

            if (firstPassword == secondPassword)
            {
                bool result;
                try
                {
                    // NOTE: do not store the database object because we don't want to keep
                    //  an open connection while we're on the application main menu (unauthenticated)
                    _ = Database.Create(firstPassword);

                    AnsiConsole.Write('\n');
                    AnsiConsole.MarkupLine($"[{ColorPalette.SUCCESS}]Succesfully created credentials database![/]");
                    result = true;
                }
                catch (Exception e)
                {
                    AnsiConsole.WriteLine(e.Message);
                    AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]Failed to create credentials database![/]");
                    result = false;
                }

                ContinuationPrompt();
                ConsoleHelper.DeleteCheckpoint();
                return result;
            }
            else
            {
                AnsiConsole.Write('\n');
                AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]The provided passwords do not match, please retry.[/]");

                ContinuationPrompt();
                ConsoleHelper.RestoreCheckpoint();
            }
        }
    }


    private static void Action_ChangeMasterPassword()
    {
        // Action title
        AnsiConsole.MarkupLine($"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]CHANGE MASTER PASSWORD[/]");

        // Authenticate the user and load the database
        string currentPassword = AuthenticationPrompt();
        AnsiConsole.Write('\n');

        if (string.IsNullOrEmpty(currentPassword) || database is null)
        {
            ContinuationPrompt();
            return;
        }

        string firstPassword = ValidatedTextPrompt(
            new TextPrompt<string>($"Choose your new [{ColorPalette.SECONDARY}]master password[/]: ")
                .Secret()
                .ClearOnFinish(false),
            new MasterPasswordValidator()
            );

        string secondPassword = ValidatedTextPrompt(
            new TextPrompt<string>($"Confirm your new [{ColorPalette.SECONDARY}]master password[/]: ")
                .Secret()
                .ClearOnFinish(false),
            new BasicValidator()
            );

        if (firstPassword == secondPassword)
        {
            // Update the master password in the database (re-encrypt all data)
            if (database.ChangePassword(currentPassword, firstPassword))
            {
                AnsiConsole.Write('\n');
                AnsiConsole.MarkupLine($"[{ColorPalette.SUCCESS}]Succesfully updated master password![/]");
            }
            else
            {
                AnsiConsole.Write('\n');
                AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]Failed to update the master password![/]");
            }
        }
        else
        {
            AnsiConsole.Write('\n');
            AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]The provided passwords do not match.[/] ");
        }

        ContinuationPrompt();
    }


    private static List<EncryptedCredential>? Action_ListCredentials()
    {
        // Action title
        AnsiConsole.MarkupLine($"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]VIEW CREDENTIALS[/]");

        // Authenticate the user
        string masterPassword = AuthenticationPrompt();
        AnsiConsole.Write('\n');

        if (string.IsNullOrEmpty(masterPassword) || database is null)
        {
            ContinuationPrompt();
            return null;
        }

        // Retrieve the list of credentials from the database
        List<EncryptedCredential> credentials = database.GetCredentials();

        return credentials;
    }


    private static List<EncryptedCredential>? Action_SearchCredentials()
    {
        // Action title
        AnsiConsole.MarkupLine($"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]SEARCH CREDENTIALS[/]");

        // Authenticate the user
        string masterPassword = AuthenticationPrompt();
        AnsiConsole.Write('\n');

        if (string.IsNullOrEmpty(masterPassword) || database is null)
        {
            ContinuationPrompt();
            return null;
        }

        // Get the search string from user input
        string searchString = ValidatedTextPrompt(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}]Search[/] for:")
                .ClearOnFinish(true),
            new BasicValidator()
            );

        // Retrieve the list of credentials from the database
        List<EncryptedCredential> credentials = database.Search(searchString);

        return credentials;
    }


    private static void Action_ViewCredential(EncryptedCredential credential)
    {
        // Action title
        AnsiConsole.MarkupLine($"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]VIEW CREDENTIAL INFORMATION[/]");

        // Authenticate the user
        string masterPassword = AuthenticationPrompt();
        AnsiConsole.Write('\n');

        if (string.IsNullOrEmpty(masterPassword) || database is null)
        {
            ContinuationPrompt();
            return;
        }

        // Generate decryption key from user password
        byte[] salt = database.GetEncryptionSalt();
        byte[] key = PBKDF2.GenerateKey(masterPassword, salt);

        // Display descrypted credential information in a table
        PlaintextCredential plaintextCredential = credential.Decrypt(key);

        Table table = new();
        table.Title($"[{ColorPalette.PRIMARY} bold]Credential Information[/]");
        table.Border(TableBorder.Heavy);
        table.AddColumn("Key", c => c.Alignment(Justify.Right));
        table.AddColumn("Value", c => c.Alignment(Justify.Center));
        table.AddRow($"[{ColorPalette.SECONDARY}]Website/Service[/]", plaintextCredential.Name);
        table.AddRow($"[{ColorPalette.SECONDARY}]Email Address[/]", plaintextCredential.EmailAddress ?? string.Empty);
        table.AddRow($"[{ColorPalette.SECONDARY}]Username[/]", plaintextCredential.Username ?? string.Empty);
        table.AddRow($"[{ColorPalette.SECONDARY}]Password[/]", plaintextCredential.Password);
        table.HideHeaders();
        table.ShowRowSeparators();

        AnsiConsole.Write(table);
        AnsiConsole.Write('\n');

        ContinuationPrompt();
    }


    private static void Action_AddCredential()
    {
        // Action title
        AnsiConsole.MarkupLine($"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]ADD ACCOUNT[/]");

        // Authenticate the user and load the database
        string masterPassword = AuthenticationPrompt();
        AnsiConsole.Write('\n');

        if (string.IsNullOrEmpty(masterPassword) || database is null)
        {
            ContinuationPrompt();
            return;
        }

        AnsiConsole.WriteLine("Please insert the required account information:");
        AnsiConsole.Write('\n');

        PlaintextCredential plaintextCredential = new()
        {
            Name = ValidatedTextPrompt(
                new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Website/Service:[/]")
                    .ClearOnFinish(false),
                new AccountNameValidator(database)
            ),
            EmailAddress = ValidatedTextPrompt(
                new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Email Address:[/]")
                    .AllowEmpty()
                    .ClearOnFinish(false),
                new EmailAddressValidator()
            ),
            Username = ValidatedTextPrompt(
                new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Username:[/]")
                    .AllowEmpty()
                    .ClearOnFinish(false),
                new BasicValidator()
            ),
            Password = ValidatedTextPrompt(
                new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Password:[/]")
                    .Secret()
                    .ClearOnFinish(false),
                new PasswordValidator()
            )
        };

        byte[] encryptionSalt = database.GetEncryptionSalt();
        byte[] encryptionKey = PBKDF2.GenerateKey(masterPassword, encryptionSalt);

        // Encrypt the account information before adding it to the database
        var encryptedCredential = plaintextCredential.Encrypt(encryptionKey);

        if (database.Insert(encryptedCredential))
            AnsiConsole.MarkupLine($"\n[{ColorPalette.SUCCESS}]Credentials succesfully added to the database![/]");
        else AnsiConsole.MarkupLine($"\n[{ColorPalette.ERROR}]Failed to add the credentials to the database[/]");

        ContinuationPrompt();
    }


    private static void Action_EditCredential(EncryptedCredential credential)
    {
        // Action title
        AnsiConsole.MarkupLine($"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]EDIT CREDENTIAL[/]");

        // Authenticate the user
        string masterPassword = AuthenticationPrompt();
        AnsiConsole.Write('\n');

        if (string.IsNullOrEmpty(masterPassword) || database is null)
        {
            ContinuationPrompt();
            return;
        }

        AnsiConsole.WriteLine("Update the credentials information:");
        AnsiConsole.Write('\n');

        byte[] encryptionSalt = database.GetEncryptionSalt();
        byte[] encryptionKey = PBKDF2.GenerateKey(masterPassword, encryptionSalt);

        // Decrypt the previous credential information to use as default values
        var plaintextCredential = credential.Decrypt(encryptionKey);

        plaintextCredential.Name = ValidatedTextPrompt(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Website/Service:[/]")
                .DefaultValue(plaintextCredential.Name)
                .EditableDefaultValue(true)
                .HideDefaultValue()
                .ClearOnFinish(false),
            new AccountNameEditValidator(database, plaintextCredential.Name)
            );

        plaintextCredential.EmailAddress = ValidatedTextPrompt(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Email Address:[/]")
                .DefaultValue(plaintextCredential.EmailAddress ?? string.Empty)
                .EditableDefaultValue(true)
                .HideDefaultValue()
                .AllowEmpty()
                .ClearOnFinish(false),
            new EmailAddressValidator()
            );

        plaintextCredential.Username = ValidatedTextPrompt(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Username:[/]")
                .DefaultValue(plaintextCredential.Username ?? string.Empty)
                .EditableDefaultValue(true)
                .HideDefaultValue()
                .AllowEmpty()
                .ClearOnFinish(false),
            new BasicValidator()
            );

        plaintextCredential.Password = ValidatedTextPrompt(
            new TextPrompt<string>($"[{ColorPalette.SECONDARY}] - Password:[/]")
                .Secret()
                .DefaultValue(plaintextCredential.Password)
                .EditableDefaultValue(true)
                .HideDefaultValue()
                .ClearOnFinish(false),
            new PasswordValidator()
            );

        // Encrypt the account information before writing it to the database
        var newEncryptedCredential = plaintextCredential.Encrypt(encryptionKey);

        if (database.Update(credential, newEncryptedCredential))
        {
            AnsiConsole.MarkupLine($"\n[{ColorPalette.SUCCESS}]Credentials succesfully updated in the database![/]");

            // Update to the new credential information in-memory
            credential.Name = newEncryptedCredential.Name;
            credential.EmailAddress = newEncryptedCredential.EmailAddress;
            credential.Username = newEncryptedCredential.Username;
            credential.EncryptionNonce = newEncryptedCredential.EncryptionNonce;
            credential.EncryptedPassword = newEncryptedCredential.EncryptedPassword;
            credential.EncryptionTag = newEncryptedCredential.EncryptionTag;
        }
        else AnsiConsole.MarkupLine($"\n[{ColorPalette.ERROR}]Failed to update the credentials in the database[/]");

        ContinuationPrompt();
    }


    private static void Action_DeleteCredential(EncryptedCredential credential, List<EncryptedCredential> credentials)
    {
        // Action title
        AnsiConsole.MarkupLine($"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]DELETE CREDENTIAL[/]");

        // Authenticate the user
        string masterPassword = AuthenticationPrompt();
        AnsiConsole.Write('\n');

        if (string.IsNullOrEmpty(masterPassword) || database is null)
        {
            ContinuationPrompt();
            return;
        }

        if (database.Remove(credential))
        {
            AnsiConsole.MarkupLine($"[{ColorPalette.SUCCESS}]Credentials succesfully removed from the database![/]");

            // Remove the deleted credential from the in-memory list
            credentials.Remove(credential);
        }
        else AnsiConsole.MarkupLine($"[{ColorPalette.ERROR}]Failed to remove the credentials from the database[/]");

        ContinuationPrompt();
    }
}
