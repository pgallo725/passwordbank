using Spectre.Console;

namespace passwordbank
{
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
                MainMenuAction.View           => "[green3]View[/] the list of all stored credentials",
                MainMenuAction.Search         => "[green3]Search[/] for an account in your stored credentials",
                MainMenuAction.Add            => "[green3]Add[/] new account credentials to the database",
                MainMenuAction.ChangePassword => "[green3]Change[/] your master password",
                MainMenuAction.Quit           => "[green3]Exit[/] PasswordBank",
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
                CredentialAction.View   => "[green3]View[/] the account credentials",
                CredentialAction.Edit   => "[green3]Edit[/] the account credentials",
                CredentialAction.Delete => "[green3]Delete[/] the account credentials from the database",
                CredentialAction.Cancel => "[green3]Back[/] to the list of credentials",
                _ => throw new ArgumentOutOfRangeException(nameof(action)),
            };
        }


        static (bool, string?) EmailAddressValidator(string emailAddress)
        {
            // Consider an email address correct if it can be used to construct a valid MailAddress object
            if (System.Net.Mail.MailAddress.TryCreate(emailAddress, out var _))
                return (true, string.Empty);
            else return (false, "The input does not match the format of a valid email address.");
        }

        static (bool, string?) AccountNameValidator(string accountName)
        {
            // Check if the account already exists in the database
            if (database is null)
                return (false, "Cannot access the database for checking the username.");
            else if (database.Contains(accountName))
                return (false, "An account with the same name already exists in the database.");
            else return (true, string.Empty);
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
            Foreground = Color.Orange1,
            Decoration = Decoration.None
        };


        static void Main(string[] args)
        {
            // Application title
            Console.Title = "PasswordBank v0.1";
            AnsiConsole.MarkupLine("""
                [green3]
                      _____                                           _  ____                 _    
                     |  __ \                                         | ||  _ \               | |   
                     | |__) |__ _  ___  ___ __      __ ___   _ __  __| || |_) |  __ _  _ __  | | __
                     |  ___// _` |/ __|/ __|\ \ /\ / // _ \ | '__|/ _` ||  _ <  / _` || '_ \ | |/ /
                     | |   | (_| |\__ \\__ \ \ V  V /| (_) || |  | (_| || |_) || (_| || | | ||   < 
                     |_|    \__,_||___/|___/  \_/\_/  \___/ |_|   \__,_||____/  \__,_||_| |_||_|\_\
                [/][darkorange3_1]
                   Version 0.1                                                      developed by JackaL
                [/][green3]
                 ________________________________________________________________________________________
                [/]
                """);

            // Trigger database creation workflow if 'credentials.db' file is not found
            if (!File.Exists(Database.DatabaseFilePath))
            {
                bool success = Action_CreateDatabase();
                if (!success)
                    return;
            }

            // Application loop
            while (true)
            {
                int menuStartRow = ConsoleHelper.GetCurrentRow();
                try
                {
                    switch (MainMenuPrompt())
                    {
                        case MainMenuAction.View:
                        {
                            Action_ListCredentials();
                            // TODO
                        } break;

                        case MainMenuAction.Search:
                        {
                            // TODO
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
                    // Print error message, wait for user confirmation and reset main menu
                    AnsiConsole.MarkupLineInterpolated($"\n[red][[ERROR]] {e.Message}[/]");
                    ContinuationPrompt();
                    ConsoleHelper.ClearLastRows(menuStartRow);
                }
            }
        }



        private static MainMenuAction MainMenuPrompt()
        {
            return AnsiConsole.Prompt(
                new SelectionPrompt<MainMenuAction>()
                    .Title("Select one of the available [darkorange]operations[/]: ")
                    .UseConverter(MainMenuActionTextConverter)
                    .AddChoices(MainMenuAction.View,
                                MainMenuAction.Search,
                                MainMenuAction.Add,
                                MainMenuAction.ChangePassword,
                                MainMenuAction.Quit)
                    .AddCancelResult(MainMenuAction.Quit)
                    .HighlightStyle(HighlightStyle)
                );
        }


        private static CredentialAction CredentialActionPrompt()
        {
            return AnsiConsole.Prompt(
                new SelectionPrompt<CredentialAction>()
                    .Title("Select what to do with the selected credential record: ")
                    .UseConverter(CredentialActionTextConverter)
                    .AddChoices(CredentialAction.View,
                                CredentialAction.Edit,
                                CredentialAction.Delete,
                                CredentialAction.Cancel)
                    .AddCancelResult(CredentialAction.Cancel)
                    .HighlightStyle(HighlightStyle)
                );
        }


        private static string AuthenticationPrompt()
        {
            // Store the row number before the prompt
            int startRow = ConsoleHelper.GetCurrentRow();

            // Action title
            AnsiConsole.MarkupLine("[grey]>> AUTHENTICATION[/]\n");

            string password = AnsiConsole.Prompt(
                new TextPrompt<string>("Insert your [slowblink darkorange]master password[/]: ")
                    .Secret()
                    .ClearOnFinish(false)
                );

            // Open the database connection on the first use
            database ??= Database.Open(password);

            // Check the password against the key stored in the database
            if (database is null || !database.VerifyPassword(password))
            {
                ConsoleHelper.ClearLastRows(startRow);
                AnsiConsole.MarkupLine("[grey]>> AUTHENTICATION[/] [red]FAILED[/]");
                return string.Empty;
            }
            else
            {
                ConsoleHelper.ClearLastRows(startRow);
                AnsiConsole.MarkupLine("[grey]>> AUTHENTICATION[/] [green3]SUCCESSFUL[/]");
                return password;
            }
        }


        private static void ContinuationPrompt()
        {
            AnsiConsole.Markup("[grey]Press[/] [gray70 slowblink]Enter[/] [grey]to continue...[/]");
            ConsoleHelper.WaitForEnterKey();
        }


        private static string ValidatedTextPrompt(TextPrompt<string> prompt, Func<string, (bool, string?)> validator)
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

                (bool isValid, string? errorMessage) = validator.Invoke(input);
                if (!isValid)
                {
                    // Print error message and set the current input as the default value
                    // for the existing prompt, so that the user can edit in-place
                    AnsiConsole.MarkupLineInterpolated($"[red]{errorMessage}[/]");
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


        // TODO: review
        private static bool Action_CreateDatabase()
        {
            // Store the row number before the prompt
            int startRow = ConsoleHelper.GetCurrentRow();

            AnsiConsole.MarkupLine("Welcome to [green3]PasswordBank[/]!");
            AnsiConsole.MarkupLine("No [italic]credentials.db[/] file detected, [slowblink]initializing new database...[/]\n");

            // Repeat the following block until the user provides matching passwords
            int startRowPasswords = ConsoleHelper.GetCurrentRow();
            while (true)
            {
                string firstPassword = AnsiConsole.Prompt(
                    new TextPrompt<string>("Choose your [darkorange]master password[/]: ")
                        .Secret()
                        .ClearOnFinish(false)
                    );

                string secondPassword = AnsiConsole.Prompt(
                    new TextPrompt<string>("\tConfirm your [darkorange]master password[/]: ")
                        .Secret()
                        .ClearOnFinish(false)
                    );

                if (firstPassword == secondPassword)
                {
                    bool result;
                    try
                    {
                        Database.Create(firstPassword);
                        AnsiConsole.MarkupLine("[green3_1]Succesfully created credentials database![/]");
                        result = true;
                    }
                    catch (Exception e)
                    {
                        AnsiConsole.WriteLine(e.Message);
                        AnsiConsole.MarkupLine("[red]Failed to create credentials database![/]");
                        result = false;
                    }
                    ContinuationPrompt();
                    ConsoleHelper.ClearLastRows(startRow);
                    return result;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]The provided passwords do not match, please retry.[/]");
                    ContinuationPrompt();
                    ConsoleHelper.ClearLastRows(startRowPasswords);
                }
            }
        }


        private static void Action_ListCredentials()
        {
            // Store the row number before the prompt
            int startRow = ConsoleHelper.GetCurrentRow();

            // Action title
            AnsiConsole.MarkupLine("[grey]>> OPERATION[/] [green3]VIEW CREDENTIALS[/]");

            // Authenticate the user
            string masterPassword = AuthenticationPrompt();
            AnsiConsole.Write('\n');

            if (string.IsNullOrEmpty(masterPassword) || database is null)
            {
                ContinuationPrompt();
                ConsoleHelper.ClearLastRows(startRow);
                return;
            }

            // Retrieve the list of credentials from the database
            List<EncryptedCredential> credentials = database.GetCredentials();

            if (credentials.Count == 0)
            {
                AnsiConsole.WriteLine("The credentials database is empty.\n");
                ContinuationPrompt();
            }
            else
            {
                /*
                 * TODO: take the following code out into a function
                 * so that it can be reused by Action_SearchCredentials as well
                 * 
                 */

                // Create a 'null' EncryptedCredential object to use as cancellation result
                EncryptedCredential nullCredential = new()
                {
                    Name = string.Empty,
                    EmailAddress = null,
                    Username = null,
                    EncryptedPassword = [],
                    EncryptionIV = []
                };

                while (true)
                {
                    // Display the list of credentials and let the user select one
                    EncryptedCredential credential = AnsiConsole.Prompt(
                        new SelectionPrompt<EncryptedCredential>()
                            .Title("Here's the list of all credentials stored in the database: ")
                            .UseConverter(c => c.Name)
                            .AddChoices(credentials)
                            .AddCancelResult(nullCredential)
                            .PageSize(10)
                            .MoreChoicesText("[grey](Use arrow keys to see more credentials)[/]")
                            .WrapAround(true)
                            .HighlightStyle(HighlightStyle)
                        );

                    if (credential == nullCredential)
                        break;

                    int credentialMenuStartRow = ConsoleHelper.GetCurrentRow();

                    switch (CredentialActionPrompt())
                    {
                        case CredentialAction.View:
                            Action_ViewCredential(credential);
                            break;

                        case CredentialAction.Edit:
                            Action_EditCredential(credential);
                            break;

                        case CredentialAction.Delete:
                            Action_DeleteCredential(credential);
                            // TODO: need to update the list of credentials after a delete operation
                            break;

                        case CredentialAction.Cancel:
                            break;
                    }

                    ConsoleHelper.ClearLastRows(credentialMenuStartRow);
                }
            }

            ConsoleHelper.ClearLastRows(startRow);
        }


        private static void Action_ViewCredential(EncryptedCredential credential)
        {
            // Store the row number before the prompt
            int startRow = ConsoleHelper.GetCurrentRow();

            // Action title
            AnsiConsole.MarkupLine("[grey]>> OPERATION[/] [green3]VIEW CREDENTIAL INFORMATION[/]");

            // Authenticate the user
            string masterPassword = AuthenticationPrompt();
            AnsiConsole.Write('\n');

            if (string.IsNullOrEmpty(masterPassword) || database is null)
            {
                ContinuationPrompt();
                ConsoleHelper.ClearLastRows(startRow);
                return;
            }

            // Generate decryption key from user password
            byte[] salt = database.GetEncryptionSalt();
            byte[] key = PBKDF2.GenerateKey(masterPassword, salt);

            // Display descrypted credential information in a table
            CredentialInfo credentialInfo = credential.Decrypt(key);

            Table table = new();
            table.Title("[green3 bold]Credential Information[/]");
            table.Border(TableBorder.Heavy);
            table.AddColumn("Key", c => c.Alignment(Justify.Right));
            table.AddColumn("Value", c => c.Alignment(Justify.Center));
            table.AddRow("[darkorange3]Website/Service[/]", credentialInfo.Name);
            table.AddRow("[darkorange3]Email Address[/]", credentialInfo.EmailAddress ?? string.Empty);
            table.AddRow("[darkorange3]Username[/]", credentialInfo.Username ?? string.Empty);
            table.AddRow("[darkorange3]Password[/]", credentialInfo.Password);
            table.HideHeaders();
            table.ShowRowSeparators();

            AnsiConsole.Write(table);
            AnsiConsole.Write('\n');

            ContinuationPrompt();
            ConsoleHelper.ClearLastRows(startRow);
        }


        private static void Action_AddCredential()
        {
            // Store the row number before the prompt
            int startRow = ConsoleHelper.GetCurrentRow();

            // Action title
            AnsiConsole.MarkupLine("[grey]>> OPERATION[/] [green3]ADD ACCOUNT[/]");

            // Authenticate the user and load the database
            string masterPassword = AuthenticationPrompt();
            AnsiConsole.Write('\n');

            if (string.IsNullOrEmpty(masterPassword) || database is null)
            {
                ContinuationPrompt();
                ConsoleHelper.ClearLastRows(startRow);
                return;
            }

            AnsiConsole.WriteLine("Please insert the required account information:");
            AnsiConsole.Write('\n');

            string accountName = ValidatedTextPrompt(
                new TextPrompt<string>("[darkorange3] - Website/Service:[/]")
                    .ClearOnFinish(false),
                AccountNameValidator
                );

            string emailAddress = ValidatedTextPrompt(
                new TextPrompt<string>("[darkorange3] - Email Address:[/]")
                    .AllowEmpty()
                    .ClearOnFinish(false),
                EmailAddressValidator
                );

            string username = AnsiConsole.Prompt(
                new TextPrompt<string>("[darkorange3] - Username:[/]")
                    .AllowEmpty()
                    .ClearOnFinish(false)
                );

            string password = AnsiConsole.Prompt(
                new TextPrompt<string>("[darkorange3] - Password:[/]")
                    .Secret()
                    .ClearOnFinish(false)
                );

            CredentialInfo credentialInfo = new()
            {
                Name = accountName,
                EmailAddress = string.IsNullOrWhiteSpace(emailAddress) ? null : emailAddress,
                Username = string.IsNullOrWhiteSpace(username) ? null : username,
                Password = password
            };

            byte[] encryptionSalt = database.GetEncryptionSalt();
            byte[] encryptionKey = PBKDF2.GenerateKey(masterPassword, encryptionSalt);

            // Encrypt the account information before adding it to the database
            var credential = credentialInfo.Encrypt(encryptionKey);

            if (database.Insert(credential))
                AnsiConsole.MarkupLine("\n[green3_1]Credentials succesfully added to the database![/]");
            else AnsiConsole.MarkupLine("\n[red]Failed to add the credentials to the database[/]");

            ContinuationPrompt();
            ConsoleHelper.ClearLastRows(startRow);
        }


        private static void Action_EditCredential(EncryptedCredential credential)
        {
            // Store the row number before the prompt
            int startRow = ConsoleHelper.GetCurrentRow();

            // Action title
            AnsiConsole.MarkupLine("[grey]>> OPERATION[/] [green3]EDIT CREDENTIAL[/]");

            // Authenticate the user
            string masterPassword = AuthenticationPrompt();
            AnsiConsole.Write('\n');

            if (string.IsNullOrEmpty(masterPassword) || database is null)
            {
                ContinuationPrompt();
                ConsoleHelper.ClearLastRows(startRow);
                return;
            }

            AnsiConsole.WriteLine("Update the credentials information:");
            AnsiConsole.Write('\n');

            byte[] encryptionSalt = database.GetEncryptionSalt();
            byte[] encryptionKey = PBKDF2.GenerateKey(masterPassword, encryptionSalt);

            // Decrypt the previous credential information to use as default values
            var credentialInfo = credential.Decrypt(encryptionKey);

            // NOTE: use a special account name validator that avoids returning an error
            //  if the account name is not modified (which would return an error with the default validator)
            (bool, string?) ModifiedAccountNameValidator(string input)
            {
                if (string.Compare(input, credentialInfo.Name, StringComparison.OrdinalIgnoreCase) == 0)
                    return (true, string.Empty);
                else return AccountNameValidator(input);
            }

            string accountName = ValidatedTextPrompt(
                new TextPrompt<string>("[darkorange3] - Website/Service:[/]")
                    .DefaultValue(credentialInfo.Name)
                    .EditableDefaultValue(true)
                    .HideDefaultValue()
                    .ClearOnFinish(false),
                ModifiedAccountNameValidator
                );

            string emailAddress = ValidatedTextPrompt(
                new TextPrompt<string>("[darkorange3] - Email Address:[/]")
                    .DefaultValue(credentialInfo.EmailAddress ?? string.Empty)
                    .EditableDefaultValue(true)
                    .HideDefaultValue()
                    .AllowEmpty()
                    .ClearOnFinish(false),
                EmailAddressValidator
                );

            string username = AnsiConsole.Prompt(
                new TextPrompt<string>("[darkorange3] - Username:[/]")
                    .DefaultValue(credentialInfo.Username ?? string.Empty)
                    .EditableDefaultValue(true)
                    .HideDefaultValue()
                    .AllowEmpty()
                    .ClearOnFinish(false)
                );

            string password = AnsiConsole.Prompt(
                new TextPrompt<string>("[darkorange3] - Password:[/]")
                    .Secret()
                    .DefaultValue(credentialInfo.Password)
                    .EditableDefaultValue(true)
                    .HideDefaultValue()
                    .ClearOnFinish(false)
                );

            CredentialInfo updatedCredentialInfo = new()
            {
                Name = accountName,
                EmailAddress = string.IsNullOrWhiteSpace(emailAddress) ? null : emailAddress,
                Username = string.IsNullOrWhiteSpace(username) ? null : username,
                Password = password
            };

            // Encrypt the account information before writing it to the database
            var updatedCredential = updatedCredentialInfo.Encrypt(encryptionKey);

            if (database.Update(credential, updatedCredential))
                AnsiConsole.MarkupLine("\n[green3_1]Credentials succesfully updated in the database![/]");
            else AnsiConsole.MarkupLine("\n[red]Failed to update the credentials in the database[/]");

            ContinuationPrompt();
            ConsoleHelper.ClearLastRows(startRow);
        }


        private static void Action_DeleteCredential(EncryptedCredential credential)
        {
            // Store the row number before the prompt
            int startRow = ConsoleHelper.GetCurrentRow();

            // Action title
            AnsiConsole.MarkupLine("[grey]>> OPERATION[/] [green3]DELETE CREDENTIAL[/]");

            // Authenticate the user
            string masterPassword = AuthenticationPrompt();
            AnsiConsole.Write('\n');

            if (string.IsNullOrEmpty(masterPassword) || database is null)
            {
                ContinuationPrompt();
                ConsoleHelper.ClearLastRows(startRow);
                return;
            }

            if (database.Remove(credential))
                AnsiConsole.MarkupLine("[green3_1]Credentials succesfully removed from the database![/]");
            else AnsiConsole.MarkupLine("[red]Failed to remove the credentials from the database[/]");

            ContinuationPrompt();
            ConsoleHelper.ClearLastRows(startRow);
        }


        private static void Action_ChangeMasterPassword()
        {
            // Store the row number before the prompt
            int startRow = ConsoleHelper.GetCurrentRow();

            // Action title
            AnsiConsole.MarkupLine("[grey]>> OPERATION[/] [green3]CHANGE MASTER PASSWORD[/]");

            // Authenticate the user and load the database
            string currentPassword = AuthenticationPrompt();
            AnsiConsole.Write('\n');

            if (string.IsNullOrEmpty(currentPassword) || database is null)
            {
                ContinuationPrompt();
                ConsoleHelper.ClearLastRows(startRow);
                return;
            }

            string firstPassword = AnsiConsole.Prompt(
                new TextPrompt<string>("Choose your new [darkorange]master password[/]: ")
                    .Secret()
                    .ClearOnFinish(false)
                );

            string secondPassword = AnsiConsole.Prompt(
                new TextPrompt<string>("\tConfirm your new [darkorange]master password[/]: ")
                    .Secret()
                    .ClearOnFinish(false)
                );

            if (firstPassword == secondPassword)
            {
                // Update the master password in the database (re-encrypt all data)
                if (database.ChangePassword(currentPassword, firstPassword))
                {
                    AnsiConsole.Write('\n');
                    AnsiConsole.MarkupLine("[green3_1]Succesfully updated master password![/]");
                }
                else
                {
                    AnsiConsole.Write('\n');
                    AnsiConsole.MarkupLine("[red3_1]Failed to update the master password![/]");
                }
            }
            else
            {
                AnsiConsole.Write('\n');
                AnsiConsole.MarkupLine("[red3_1]The provided passwords do not match.[/] ");
            }

            ContinuationPrompt();
            ConsoleHelper.ClearLastRows(startRow);
        }
    }
}
