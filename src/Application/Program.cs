using PasswordBank.DataModel;
using PasswordBank.UI;
using Spectre.Console;
using System.Diagnostics;

namespace PasswordBank;

internal partial class Program
{
    /// <summary>
    /// The PasswordBank application version number in the <i>Major.Minor.Patch</i> format.
    /// </summary>
    private static readonly Version VersionNumber = new(1, 0, 0);

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
        Console.Title = $"PasswordBank v{VersionNumber}";
        AnsiConsole.Write(new Panel($"""
            [{ColorPalette.PRIMARY}]
                _____                                           _  ____                 _    
               |  __ \                                         | ||  _ \               | |   
               | |__) |__ _  ___  ___ __      __ ___   _ __  __| || |_) |  __ _  _ __  | | __
               |  ___// _` |/ __|/ __|\ \ /\ / // _ \ | '__|/ _` ||  _ <  / _` || '_ \ | |/ /
               | |   | (_| |\__ \\__ \ \ V  V /| (_) || |  | (_| || |_) || (_| || | | ||   < 
               |_|    \__,_||___/|___/  \_/\_/  \___/ |_|   \__,_||____/  \__,_||_| |_||_|\_\[/]
            [{ColorPalette.SECONDARY}]
            Version {VersionNumber}                                                    developed by JackaL[/]

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
            try
            {
                if (!CreateDatabase())
                    return;
            }
            finally
            {
                ConsoleHelper.RestoreCheckpoint();
                ConsoleHelper.DeleteCheckpoint();
            }
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
                        .HighlightStyle(Styles.HIGHLIGHT)
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
                            MainMenuAction.View => ListCredentials(),
                            MainMenuAction.Search => SearchCredentials(),
                            _ => throw new UnreachableException()
                        };
                        if (credentials is null)  // (authentication failed)
                        {
                            ContinuationPrompt();
                            ConsoleHelper.RestoreCheckpoint();
                            break;
                        }
                        ConsoleHelper.RestoreCheckpoint();

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
                                    .UseConverter(c => Markup.Escape(c.Name))
                                    .AddChoices(credentials)
                                    .AddCancelResult(NullCredential)
                                    .PageSize(10)
                                    .MoreChoicesText($"[{ColorPalette.NEUTRAL}](Use arrow keys to see more credentials)[/]")
                                    .WrapAround(true)
                                    .HighlightStyle(Styles.HIGHLIGHT)
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
                                    .HighlightStyle(Styles.HIGHLIGHT)
                            );
                            // Clear the menu before continuing into the selected action
                            ConsoleHelper.RestoreCheckpoint();

                            switch (credentialAction)
                            {
                                case CredentialAction.View:
                                    ViewCredential(credential);
                                    ContinuationPrompt();
                                    break;

                                case CredentialAction.Edit:
                                    EditCredential(credential);
                                    ContinuationPrompt();
                                    break;

                                case CredentialAction.Delete:
                                    DeleteCredential(credential, credentials);
                                    ContinuationPrompt();
                                    break;

                                case CredentialAction.Cancel:
                                    /* Nothing to do, show the previous menu */
                                    break;
                            }
                            // Clear everything before showing the (updated) list of credentials again
                            ConsoleHelper.RestoreCheckpoint();
                        }
                    } break;

                    case MainMenuAction.Add:
                        AddCredential();
                        ContinuationPrompt();
                        break;

                    case MainMenuAction.ChangePassword:
                        ChangeMasterPassword();
                        ContinuationPrompt();
                        break;

                    case MainMenuAction.Quit:
                        // Nothing to do, just exit from the application loop
                        return;
                }
            }
            catch (Exception e)
            {
            #if DEBUG   // in Debug mode, print the whole exception trace
            #pragma warning disable IL3050  // WriteException() only used in Debug, no need for AOT compilation
                AnsiConsole.Write('\n');
                AnsiConsole.WriteException(e, Styles.EXCEPTION);
            #pragma warning restore IL3050
            #elif RELEASE   // in Release mode, print only a short summary of the error
                AnsiConsole.MarkupLineInterpolated($"\n[{ColorPalette.ERROR}][[ERROR]] {e.Message}[/]");
            #endif
                // Wait for confirmation then go reset to the main menu
                ContinuationPrompt();
            }

            // Clear everything before showing the main menu again
            ConsoleHelper.RestoreCheckpoint();
        }
    }
}
