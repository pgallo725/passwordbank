using Spectre.Console;

namespace passwordbank
{
    internal class ConsoleHelper
    {
        public static string SafeInput()
        {
            int i = 0;
            char[] input = new char[256];
            
            while (i < input.Length)
            {
                ConsoleKeyInfo keypress = Console.ReadKey(true);
                char keyChar = keypress.KeyChar;

                // Skip unsupported modifiers
                if ((keypress.Modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt ||
                    (keypress.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
                    continue;

                // Skip keys that do not map to Unicode characters
                if (keyChar == '\u0000')
                    continue;

                // Handle Enter keypress
                if (keypress.Key == ConsoleKey.Enter)
                    break;

                // Handle Backspace keypress
                if (keypress.Key == ConsoleKey.Backspace)
                {
                    if (i >= 1)
                    {
                        Console.CursorLeft -= 1;
                        Console.Write(' ');
                        Console.CursorLeft -= 1;
                        i--;
                    }
                    continue;
                }

                // Add regular ASCII characters to the string and echo a '*'
                if (keyChar > 31 && keyChar < 128)
                {
                    input[i++] = keyChar;
                    Console.Write('*');
                }
            }
            Console.Write('\n');

            return new string(input, 0, i);
        }


        public static void WaitForEnterKey()
        {
            while (true)
            {
                ConsoleKeyInfo keypress = Console.ReadKey(true);
                if (keypress.Key == ConsoleKey.Enter)
                    break;
            }
        }


        public static int GetCurrentRow()
        {
            (int _, int row) = Console.GetCursorPosition();
            return row;
        }

        public static void ClearLastRows(int startRow)
        {
            int currentRow = GetCurrentRow();
            int nRowsToClear = Math.Clamp(currentRow - startRow, 0, int.MaxValue);
            string clearLine = new(' ', Console.WindowWidth);

            AnsiConsole.Write('\r');                       // return to line start
            AnsiConsole.Write($"\x1b[{nRowsToClear+1}A");  // move cursor back to startRow
            for (int i = 0; i <= nRowsToClear; i++)        // repeat for nRowsToClear+1 lines
                AnsiConsole.WriteLine(clearLine);          // overwrite the whole line with whitespace characters
            AnsiConsole.Write($"\x1b[{nRowsToClear+1}A");  // move cursor back to startRow
        }
    }
}
