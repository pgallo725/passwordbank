using PasswordBank.DataModel;
using PasswordBank.UI;
using Spectre.Console;

namespace PasswordBank;

internal partial class Program
{
    /// <summary>
    /// The PasswordBank application version number in the <i>Major.Minor.Patch</i> format.
    /// </summary>
    private static readonly Version VersionNumber = new(1, 1, 1);


    static void Main(string[] args)
    {
        if (AnsiConsole.Profile.Capabilities.Ansi == false)
            throw new ApplicationException("PasswordBank requires an ANSI-capable console, and the current one isn't.");

        Console.Title = $"PasswordBank v{VersionNumber}";

        // Display the application UI using the terminal's alternate screen buffer
        AnsiConsole.AlternateScreen(delegate ()
        {
            AnsiConsole.Clear();
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
            AnsiConsole.Cursor.Hide();
            AnsiConsole.WriteLine();


            // Trigger database creation workflow if 'credentials.db' file is not found
            if (!File.Exists(Database.DatabaseFilePath))
            {
                ConsoleHelper.SaveCheckpoint();
                try
                {
                    AnsiConsole.MarkupLine($"""
                        [{ColorPalette.NEUTRAL_LIGHT}]File [italic]'credentials.db'[/] not detected in the application folder.[/]
                        [{ColorPalette.NEUTRAL}]Executing one-time database creation procedure...[/]

                        """);

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
            bool quit = false;

            // Main application loop
            while (!quit)
            {
                try
                {
                    quit = MainMenu();
                }
                catch (Exception e)
                {
                #if DEBUG   // in Debug mode, print the whole exception trace
                #pragma warning disable IL3050  // WriteException() only used in Debug, no need for AOT compilation
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteException(e, Styles.EXCEPTION);
                #pragma warning restore IL3050
                #elif RELEASE   // in Release mode, print only a short summary of the error
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLineInterpolated($"[{ColorPalette.ERROR}][[ERROR]] {e.Message}[/]");
                #endif
                    // Wait for confirmation then reset to the main menu
                    ContinuationPrompt();
                }
                finally
                {
                    // Clear everything before showing the main menu again
                    ConsoleHelper.RestoreCheckpoint();
                }
            }
            ConsoleHelper.DeleteCheckpoint();
        });
    }
}
