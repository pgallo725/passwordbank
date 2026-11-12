using PasswordBank.DataModel;
using PasswordBank.UI;
using Spectre.Console;
using System.Globalization;
using System.Text;

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
        GroupByEmail,
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
            MainMenuAction.GroupByEmail   => $"[{ColorPalette.PRIMARY}]Group[/] credential records by email address",
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
    /// A 'null' EncryptedCredential object to use as cancellation result.
    /// </summary>
    private static readonly EncryptedCredential NullCredential = new()
    {
        Name = "[BACK TO MAIN MENU]",
        EmailAddress = null,
        Username = null,
        EncryptionNonce = [],
        EncryptedPassword = [],
        EncryptionTag = []
    };


    internal static bool MainMenu()
    {
        ConsoleHelper.SaveCheckpoint();
        try
        {
            // Show the main menu
            MainMenuAction mainAction = AnsiConsole.Prompt(
                new SelectionPrompt<MainMenuAction>()
                    .Title($"Select one of the available [{ColorPalette.PRIMARY}]operations[/]:")
                    .UseConverter(MainMenuActionTextConverter)
                    .AddChoices(MainMenuAction.View,
                                MainMenuAction.Search,
                                MainMenuAction.Add,
                                MainMenuAction.GroupByEmail,
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
                {
                    var credentials = ListCredentials();
                    if (credentials is null)  // (authentication failed)
                    {
                        ContinuationPrompt();
                        ConsoleHelper.RestoreCheckpoint();
                        break;
                    }
                    ConsoleHelper.RestoreCheckpoint();

                    CredentialSelectionMenu(credentials,
                        $"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]LIST CREDENTIAL RECORDS[/]",
                        CompositeFormat.Parse($"[{ColorPalette.NEUTRAL}]Showing [bold]{{0}}[/] credential record(s).[/]")
                        );
                } break;

                case MainMenuAction.Search:
                {
                    var credentials = SearchCredentials();
                    if (credentials is null)  // (authentication failed)
                    {
                        ContinuationPrompt();
                        ConsoleHelper.RestoreCheckpoint();
                        break;
                    }
                    ConsoleHelper.RestoreCheckpoint();

                    CredentialSelectionMenu(credentials,
                        $"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]SEARCH CREDENTIAL RECORDS[/]",
                        CompositeFormat.Parse($"[{ColorPalette.NEUTRAL}]Search found [bold]{{0}}[/] relevant result(s).[/]")
                        );
                } break;

                case MainMenuAction.GroupByEmail:
                {
                    var groups = GroupCredentialsByEmail();
                    if (groups is null)  // (authentication failed)
                    {
                        ContinuationPrompt();
                        ConsoleHelper.RestoreCheckpoint();
                        break;
                    }
                    ConsoleHelper.RestoreCheckpoint();
                    GroupSelectionMenu(groups);
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
                    // NOTHING TO DO
                    break;
            }
            return (mainAction == MainMenuAction.Quit);
        }
        finally
        {
            // Clear everything before going back to the caller
            ConsoleHelper.RestoreCheckpoint();
            ConsoleHelper.DeleteCheckpoint();
        }
    }


    internal static void GroupSelectionMenu(Dictionary<string, List<EncryptedCredential>> groups)
    {
        ConsoleHelper.SaveCheckpoint();
        try
        {
        Start:
            // Print menu action title (every time)
            AnsiConsole.MarkupLine($"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]GROUP CREDENTIAL RECORDS (BY EMAIL)[/]");
            AnsiConsole.WriteLine();

            // Count total number of credentials
            int numCredentials = 0;
            foreach (var list in groups.Values)
                numCredentials += list.Count;

            // Display the number of distinct email addresses
            AnsiConsole.MarkupLine(
                $"[{ColorPalette.NEUTRAL}]Organized [bold]{numCredentials}[/] credential(s) into [bold]{groups.Count}[/] group(s).[/]"
                );

            // Display the list of email addresses and let the user select one
            string group = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a group to show the related credentials:")
                    .UseConverter(email => {
                        if (string.IsNullOrEmpty(email))
                            return Markup.Escape(NullCredential.Name);
                        return $"{Markup.Escape(email)} [{ColorPalette.NEUTRAL}]({groups[email].Count})[/]";
                    })
                    .AddChoices(groups.Keys)
                    .AddChoices(string.Empty)
                    .AddCancelResult(string.Empty)
                    .PageSize(9)
                    .MoreChoicesText($"[{ColorPalette.NEUTRAL}](Use the UP/DOWN arrow keys to navigate all groups)[/]")
                    .WrapAround(true)
                    .HighlightStyle(Styles.HIGHLIGHT)
            );
            // Clear the menu before continuing
            ConsoleHelper.RestoreCheckpoint();

            if (group == string.Empty)
                return;

            string title = $"[{ColorPalette.NEUTRAL}]>> OPERATION[/] [{ColorPalette.PRIMARY}]LIST CREDENTIAL RECORDS (EMAIL)[/]";
            var headerFormat = CompositeFormat.Parse($"[{ColorPalette.NEUTRAL}]Showing [bold]{{0}}[/] credential(s) from group '{group}'.[/]");

            // Show the menu to select a credential record from the list
            CredentialSelectionMenu(groups[group], title, headerFormat);

            //------------------------------------------------------------
            // Update groups
            List<string> keysToRemove = [];
            List<EncryptedCredential> valuesToInsert = [];

            foreach ((string expectedKey, List<EncryptedCredential> credentials) in groups)
            {
                List<EncryptedCredential> valuesToRemove = [];
                foreach (var credential in credentials)
                {
                    string actualKey = string.IsNullOrEmpty(credential.EmailAddress) 
                        ? "No email address" : credential.EmailAddress;

                    if (string.CompareOrdinal(actualKey, expectedKey) != 0)
                        valuesToRemove.Add(credential);
                }
                valuesToInsert.AddRange(valuesToRemove);

                foreach (var credential in valuesToRemove)
                    credentials.Remove(credential);

                if (credentials.Count == 0)
                    keysToRemove.Add(expectedKey);
            }
            foreach (var key in keysToRemove)
                groups.Remove(key);

            foreach (EncryptedCredential value in valuesToInsert)
            {
                string key = string.IsNullOrEmpty(value.EmailAddress)
                    ? "No email address" : value.EmailAddress;

                ref var newGroup = ref System.Runtime.InteropServices.CollectionsMarshal
                    .GetValueRefOrAddDefault(groups, key, out _);

                newGroup ??= [];
                newGroup.Add(value);
            }
            //------------------------------------------------------------

            ConsoleHelper.RestoreCheckpoint();
            goto Start;
        }
        finally
        {
            // Clear everything before going back to the caller
            ConsoleHelper.RestoreCheckpoint();
            ConsoleHelper.DeleteCheckpoint();
        }
    }


    internal static void CredentialSelectionMenu(List<EncryptedCredential> credentials, string title, CompositeFormat headerFormat)
    {
        if (headerFormat.MinimumArgumentCount != 1)
            throw new ArgumentException("Invalid string format template.", nameof(headerFormat));

        ConsoleHelper.SaveCheckpoint();
        try
        {
        Start:
            // Print menu action title (every time)
            AnsiConsole.MarkupLine(title);
            AnsiConsole.WriteLine();

            // Generate the header string with the up-to-date credentials count and print it
            string header = string.Format(CultureInfo.InvariantCulture, headerFormat, credentials.Count);
            AnsiConsole.MarkupLine(header);

            // Display the list of credentials and let the user select one
            EncryptedCredential credential = AnsiConsole.Prompt(
                new SelectionPrompt<EncryptedCredential>()
                    .Title("Select an item to access further options:")
                    .UseConverter(c => Markup.Escape(c.Name))
                    .AddChoices(credentials)
                    .AddChoices(NullCredential)
                    .AddCancelResult(NullCredential)
                    .PageSize(9)
                    .MoreChoicesText($"[{ColorPalette.NEUTRAL}](Use the UP/DOWN arrow keys to navigate all records)[/]")
                    .WrapAround(true)
                    .HighlightStyle(Styles.HIGHLIGHT)
            );
            // Clear the menu before continuing
            ConsoleHelper.RestoreCheckpoint();

            if (credential == NullCredential)
                return;

            // Show the menu to choose an action for the selected credential
            CredentialActionMenu(credential, credentials);

            ConsoleHelper.RestoreCheckpoint();
            goto Start;
        }
        finally
        {
            // Clear everything before going back to the caller
            ConsoleHelper.RestoreCheckpoint();
            ConsoleHelper.DeleteCheckpoint();
        }
    }


    internal static void CredentialActionMenu(EncryptedCredential credential, List<EncryptedCredential> credentialsList)
    {
        ConsoleHelper.SaveCheckpoint();
        try
        {
            // Show the menu to choose the action for the current credential
            string current = Markup.Escape(credential.Name);
            CredentialAction credentialAction = AnsiConsole.Prompt(
                new SelectionPrompt<CredentialAction>()
                    .Title($"Select one of the available [{ColorPalette.PRIMARY}]operations[/] for [{ColorPalette.NEUTRAL}]{current}[/]:")
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
                    DeleteCredential(credential, credentialsList);
                    ContinuationPrompt();
                    break;

                case CredentialAction.Cancel:
                    /* Nothing to do, leave this menu */
                    break;
            }
        }
        finally
        {
            // Clear everything before going back to the caller
            ConsoleHelper.RestoreCheckpoint();
            ConsoleHelper.DeleteCheckpoint();
        }
    }
}
