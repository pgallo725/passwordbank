using PasswordBank.Application;
using PasswordBank.DataModel;
using PasswordBank.Foundation;
using PasswordBank.UI;
using Spectre.Console;

namespace PasswordBank;

internal partial class Program
{
    /// <summary>
    /// Prompts the user to input the master password and then verifies its correctness against the database.
    /// </summary>
    /// <param name="inputPassword">The user-provided password (regardless of its correctness).</param>
    /// <returns>
    /// An open connection to the credentials database in case of success, otherwise null.
    /// </returns>
    internal static Database? AuthenticationPrompt(out SecretString inputPassword)
    {
        AnsiConsole.Markup($"[{ColorPalette.NEUTRAL}]>> AUTHENTICATION[/]");
        ConsoleHelper.SaveCheckpoint(); // to change the status message later on
        AnsiConsole.MarkupLine($" [{ColorPalette.NEUTRAL}]PENDING[/]\n");

        AnsiConsole.Cursor.Show();

        AnsiConsole.Markup($"Insert your [{ColorPalette.SECONDARY} slowblink]master password[/]: ");
        inputPassword = ConsoleHelper.SecretTextInput();

        AnsiConsole.Cursor.Hide();

        Database? database = null;
        bool isCorrectPassword;
        try
        {
            // Open the database connection with the provided password
            database = Database.Open(inputPassword);

            // Check the password against the key stored in the database
            isCorrectPassword = database?.VerifyPassword(inputPassword) ?? false;
        }
        catch (Exception)
        {
            database?.Dispose();  // close the database connection in case of failure
            return null;
        }
        finally  // ensure this is executed even if the code above throws
        {
            // NOTE: password verification is a slow operation (database access + PBKDF2)
            //  therefore we clear the console only after, to minimize perceived delay
            ConsoleHelper.RestoreCheckpoint();
            ConsoleHelper.DeleteCheckpoint();
        }

        if (isCorrectPassword)
        {
            AnsiConsole.MarkupLine($" [{ColorPalette.PRIMARY}]SUCCESSFUL[/]\n");
            return database;
        }
        else
        {
            database?.Dispose();  // close the database connection in case of failure

            AnsiConsole.MarkupLine($" [{ColorPalette.ERROR}]FAILED[/]\n");
            return null;
        }
    }
    

    /// <summary>
    /// Prompts the user to press the 'Enter' key for continuing program execution.
    /// </summary>
    internal static void ContinuationPrompt()
    {
        AnsiConsole.Markup($"""
            [{ColorPalette.NEUTRAL}]Press[/] [{ColorPalette.NEUTRAL_LIGHT} slowblink]Enter[/] [{ColorPalette.NEUTRAL}]to continue...[/]
            """);

        AnsiConsole.Cursor.Hide();
        ConsoleHelper.WaitForEnterKey();
    }


    /// <summary>
    /// Wraps a Spectre.Console text prompt by adding better input validation features,<br/>
    /// which prevent the prompt from returning until the user provides a valid input.
    /// </summary>
    /// <param name="prompt">The Spectre.Console TextPrompt object to wrap.</param>
    /// <param name="context">A context struct containing any data or state that the validator might need.</param>
    /// <typeparam name="TValidator">The type of input validator to use for this prompt.</typeparam>
    /// <returns>
    /// The (valid) text input provided by the user.
    /// </returns>
    internal static string PlaintextPrompt<TValidator>(TextPrompt<string> prompt, ValidationContext context = default)
        where TValidator : IValidator<string>
    {
        // Store the cursor position before showing the prompt
        (int column, int row) = Console.GetCursorPosition();

        // Disable built-in prompt validation
        prompt.Validator = null;
        prompt.ValidationErrorMessage = string.Empty;

        // Setup prompt to show editable default values
        prompt.ShowDefaultValue = false;
        prompt.EditableDefaultValue = true;

        while (true)
        {
            AnsiConsole.Cursor.Show();

            // Show Spectre.Console prompt to get user input
            string input = AnsiConsole.Prompt(prompt);

            AnsiConsole.Cursor.Hide();

            if (string.IsNullOrEmpty(input) && prompt.AllowEmpty)
                return input;

            ValidationResult result = TValidator.Validate(input, context);
            if (!result.Successful)
            {
                // Print error message and set the current input as the default value
                // for the existing prompt, so that the user can edit in-place
                AnsiConsole.MarkupLineInterpolated($"[{ColorPalette.ERROR}]{result.Message ?? string.Empty}[/]");
                prompt.DefaultValue(input);

                // NOTE: ANSI cursor positions are 1-based
                AnsiConsole.Cursor.SetPosition(column+1, row+1); // the new prompt will cover the previous one perfectly
            }
            else
            {
                // NOTE: the cursor is already at the left-most position
                ConsoleHelper.EraseInLine(ConsoleHelper.EraseLineMode.EntireLine);  // clear error line
                return input;
            }
        }
    }


    /// <summary>
    /// Replaces regular Spectre.Console prompts in order to support <see cref="SecretString"/> as result type.
    /// </summary>
    /// <typeparam name="TValidator">The type of input validator to use for this prompt.</typeparam>
    /// <param name="title">The text to be shown in the user-facing prompt.</param>
    /// <param name="editableValue">If provided, the input field is initialized with this value that the user can edit.</param>
    /// <param name="context">A context struct containing any data or state that the validator might need.</param>
    /// <returns>
    /// The (valid) secret text input provided by the user.
    /// </returns>
    internal static SecretString SecretPrompt<TValidator>(
        string title, SecretString? editableValue = null, ValidationContext context = default)
        where TValidator : IValidator<SecretString>
    {
        // Write prompt title
        AnsiConsole.Markup(title);
        AnsiConsole.Write(' ');

        // Store the cursor position before user input
        (int column, int row) = Console.GetCursorPosition();

        while (true)
        {
            AnsiConsole.Cursor.Show();

            // Get secret user input
            SecretString input = ConsoleHelper.SecretTextInput(editableValue);
            editableValue?.Zeroise();

            AnsiConsole.Cursor.Hide();

            // Position the cursor at the beginning of the next line
            // NOTE: ANSI cursor positions are 1-based
            AnsiConsole.Cursor.MoveDown();
            AnsiConsole.Cursor.MoveLeft(Console.GetCursorPosition().Left);

            ValidationResult result = TValidator.Validate(input, context);
            if (!result.Successful)
            {
                // Print error message and set the current input as the default value
                // for the existing prompt, so that the user can edit in-place
                AnsiConsole.MarkupLineInterpolated($"[{ColorPalette.ERROR}]{result.Message ?? string.Empty}[/]");
                editableValue = input;

                // NOTE: ANSI cursor positions are 1-based
                AnsiConsole.Cursor.SetPosition(column+1, row+1); // the new prompt will cover the previous one perfectly
            }
            else
            {
                // NOTE: the cursor is already at the left-most position
                ConsoleHelper.EraseInLine(ConsoleHelper.EraseLineMode.EntireLine);  // clear error line
                return input;
            }
        }
    }
}
