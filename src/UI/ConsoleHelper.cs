using PasswordBank.Foundation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Spectre.Console;

namespace PasswordBank.UI
{
    internal static class ConsoleHelper
    {
        public static SecretString SecretTextInput(SecretString? initialString = null)
        {
            const int MAX_SECRET_LENGTH = 128;
            Span<char> input = stackalloc char[MAX_SECRET_LENGTH];
            int length = 0;

            try
            {
                // If an initial value is provided, copy it into the working buffer
                if (!SecretString.IsNullOrEmpty(initialString))
                {
                    initialString.GetData().CopyTo(input);
                    for (length = 0; length < initialString.GetLength(); length++)
                        AnsiConsole.Write('*');
                }

                while (true)
                {
                    ConsoleKeyInfo? inputkey = AnsiConsole.Console.Input.ReadKey(true);
                    if (inputkey is null)
                        continue;

                    ConsoleKeyInfo keypress = inputkey.Value;
                    char keyChar = keypress.KeyChar;

                    // Skip unsupported modifiers
                    if ((keypress.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0)
                        continue;

                    // Handle Enter keypress
                    if (keypress.Key == ConsoleKey.Enter)
                        break;

                    // Handle Backspace keypress
                    if (keypress.Key == ConsoleKey.Backspace)
                    {
                        if (length > 0)
                        {
                            AnsiConsole.Cursor.MoveLeft();
                            AnsiConsole.Write(' ');
                            AnsiConsole.Cursor.MoveLeft();
                            length--;
                        }
                        continue;
                    }

                    // Skip non-ASCII or invalid characters
                    if (keyChar == '\u0000' || !char.IsAscii(keyChar))
                        continue;

                    // Add regular ASCII characters to the string and echo a '*'
                    if (length < MAX_SECRET_LENGTH)
                    {
                        input[length++] = keyChar;
                        AnsiConsole.Write('*');
                    }
                }

                char[] data = GC.AllocateUninitializedArray<char>(length);
                input[0..length].CopyTo(data);

                return new SecretString(data);
            }
            finally
            {
                // Zeroise local buffer to remove sensitive data from memory
                Span<byte> bytes = MemoryMarshal.AsBytes(input);
                CryptographicOperations.ZeroMemory(bytes);
            }
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


        // Corresponds to the supported values of 'n' in the "CSI n K" ANSI sequence
        public enum EraseLineMode
        {
            AfterCursor,
            BeforeCursor,
            EntireLine
        }
        public static void EraseInLine(EraseLineMode mode)
        {
            // Does NOT change the cursor position
            AnsiConsole.Write($"\x1b[{(int)mode}K");
        }


        // Corresponds to the supported values of 'n' in the "CSI n J" ANSI sequence
        public enum EraseDisplayMode
        {
            AfterCursor,
            BeforeCursor,
            EntireDisplay
        }
        public static void EraseInLine(EraseDisplayMode mode)
        {
            // Does NOT change the cursor position
            AnsiConsole.Write($"\x1b[{(int)mode}J");
        }


        private readonly struct ConsolePosition
        {
            public int Row { get; init; }
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
                ConsolePosition checkpoint = checkpoints.Peek();
                AnsiConsole.Cursor.SetPosition(checkpoint.Column + 1, checkpoint.Row + 1);
                AnsiConsole.Write("\x1b[0J");  // clear the whole screen after the cursor using ANSI codes
            }
        }

        public static void DeleteCheckpoint()
        {
            if (checkpoints.Count > 0)
                checkpoints.Pop();
        }
    }
}
