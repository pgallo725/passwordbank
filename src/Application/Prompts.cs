using PasswordBank.Application;
using PasswordBank.DataModel;
using PasswordBank.UI;
using Spectre.Console;

namespace PasswordBank;

internal partial class Program
{
    /// <summary>
    /// Prompts the user to input the master password and then verifies its correctness against the database.
    /// </summary>
    /// <param name="inputPassword">The user-provided password (regardless of its correctness).</param>
    /// <returns>An open connection to the credentials database in case of success, otherwise null.</returns>
    internal static Database? AuthenticationPrompt(out SecretString inputPassword)
    {
        // Action title
        AnsiConsole.Markup($"[{ColorPalette.NEUTRAL}]>> AUTHENTICATION[/]");

        ConsoleHelper.SaveCheckpoint();

        AnsiConsole.Write('\n');
        AnsiConsole.Write('\n');

        AnsiConsole.Markup($"Insert your [{ColorPalette.SECONDARY} slowblink]master password[/]: ");
        inputPassword = ConsoleHelper.SecretTextInput();
        AnsiConsole.Write('\n');

        Database? database;
        bool isCorrectPassword;
        try
        {
            // Open the database connection with the provided password
            database = Database.Open(inputPassword);

            // Check the password against the key stored in the database
            isCorrectPassword = database?.VerifyPassword(inputPassword) ?? false;
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

        ConsoleHelper.WaitForEnterKey();
    }


    /// <summary>
    /// Wraps a Spectre.Console text prompt by adding better input validation features,<br/>
    /// which prevent the prompt from returning until the user provides a valid input.
    /// </summary>
    /// <param name="prompt">The Spectre.Console TextPrompt object to wrap.</param>
    /// <param name="validator">An object implementing the IValidator interface for validating the input text.</param>
    /// <returns>The (valid) text input provided by the user.</returns>
    internal static string ValidatedTextPrompt(TextPrompt<string> prompt, IValidator validator)
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
                AnsiConsole.MarkupLineInterpolated($"[{ColorPalette.ERROR}]{errorMessage ?? string.Empty}[/]");
                prompt.DefaultValue(input);
                Console.SetCursorPosition(column, row); // the updated prompt will cover the existing text perfectly
            }
            else
            {
                Console.SetCursorPosition(0, row + 1);
                AnsiConsole.Write(new string(' ', Console.BufferWidth));  // clear error line
                Console.SetCursorPosition(0, row + 1);
                return input;
            }
        }
    }


    internal static SecretString ValidatedSecretPrompt(string title, IValidator validator, SecretString? editableValue = null)
    {
        // Store the row number before the prompt
        (int column, int row) = Console.GetCursorPosition();

        while (true)
        {
            // Write prompt title
            AnsiConsole.Markup(title);
            AnsiConsole.Write(' ');

            // Get secret user input
            SecretString input = ConsoleHelper.SecretTextInput(editableValue);
            editableValue?.Zeroise();

            (bool isValid, string? errorMessage) = validator.Validate(input);
            if (!isValid)
            {
                // Print error message and set the current input as the default value
                // for the existing prompt, so that the user can edit in-place
                Console.SetCursorPosition(0, row + 1);
                AnsiConsole.MarkupLineInterpolated($"[{ColorPalette.ERROR}]{errorMessage ?? string.Empty}[/]");
                editableValue = input;
                Console.SetCursorPosition(column, row); // the updated prompt will cover the existing text perfectly
            }
            else
            {
                Console.SetCursorPosition(0, row + 1);
                AnsiConsole.Write(new string(' ', Console.BufferWidth));  // clear error line
                Console.SetCursorPosition(0, row + 1);
                return input;
            }
        }
    }
}
