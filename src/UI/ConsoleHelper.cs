using System.Text;
using Spectre.Console;

namespace PasswordBank.UI
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
            checkpoints.Push(new ConsolePosition()
            {
                Row = Console.CursorTop,
                Column = Console.CursorLeft
            });
        }

        public static void RestoreCheckpoint()
        {
            if (checkpoints.Count > 0)
            {
                var currentPosition = new ConsolePosition()
                {
                    Row = Console.CursorTop,
                    Column = Console.CursorLeft
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
            int rowCount = end.Row - start.Row + 1;
            int firstRowLength = Console.BufferWidth - start.Column;
            int lastRowLength = end.Column;

            // Use a StringBuilder to minimize GC allocations
            StringBuilder clearStringBuilder = new(rowCount * (Console.BufferWidth + 1));

            // Clear the rest of the first row after start.Column
            clearStringBuilder.Append(' ', firstRowLength);

            // Clear all full rows between start.Row and end.Row
            for (int row = 0; row < rowCount - 2; row++)
            {
                clearStringBuilder.Append('\n');
                clearStringBuilder.Append(' ', Console.BufferWidth);
            }

            // Clear the part of the last row before end.Column
            if (rowCount > 1)
            {
                clearStringBuilder.Append('\n');
                clearStringBuilder.Append(' ', lastRowLength);
            }

            // Position the cursor at the start of the clear region
            Console.SetCursorPosition(start.Column, start.Row);

            // Write the clear string to the console
            // Instead of calling StringBuilder.ToString(), which allocates new memory,
            //  we write its chunks directly to the output as ReadOnlySpan<char>
            foreach (ReadOnlyMemory<char> chunk in clearStringBuilder.GetChunks())
                Console.Write(chunk.Span);
        }
    }
}
