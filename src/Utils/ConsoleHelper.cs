using Spectre.Console;

namespace PasswordBank.Utils
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


        private readonly struct ConsolePosition
        {
            public int Row    { get; init; }
            public int Column { get; init; }
        }
        private static readonly Stack<ConsolePosition> checkpoints = [];


        public static void SaveCheckpoint()
        {
            (int column, int row) = Console.GetCursorPosition();
            checkpoints.Push(new ConsolePosition()
            {
                Row = row,
                Column = column
            });
        }

        public static void RestoreCheckpoint()
        {
            if (checkpoints.Count > 0)
            {
                (int column, int row) = Console.GetCursorPosition();
                var currentPosition = new ConsolePosition()
                {
                    Row = row,
                    Column = column
                };
                var checkpointPosition = checkpoints.Peek();

                ClearRegion(checkpointPosition, currentPosition);
                Console.SetCursorPosition(checkpointPosition.Column, checkpointPosition.Row);
            }
        }

        public static void DeleteCheckpoint()
        {
            if (checkpoints.Count > 0)
                checkpoints.Pop();
        }


        private static void ClearRegion(ConsolePosition start, ConsolePosition end)
        {
            string clearLine = new(' ', Console.WindowWidth);

            // Position the cursor at the start of the clear region
            Console.SetCursorPosition(start.Column, start.Row);

            // Clear the rest of the first row after start.Column
            AnsiConsole.WriteLine(clearLine[start.Column..]);

            // Clear all full rows between start.Row and end.Row
            for (int i = 0; i < end.Row - start.Row - 1; i++)
                AnsiConsole.WriteLine(clearLine);

            // Clear the part of the last row before end.Column
            AnsiConsole.Write(clearLine[..end.Column]);
        }
    }
}
