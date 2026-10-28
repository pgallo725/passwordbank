using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PasswordBank.Foundation
{
    /// <summary>
    /// A class to replace usage of .NET strings when storing short-lived plaintext secrets in memory.<br/>
    /// The secret is automatically wiped from memory when the object is <i>disposed</i>.
    /// </summary>
    /// <remarks>
    /// <b>NOTE:</b> To get the RAII behavior, instances of this class must be declared with <see langword="using"/>
    ///   or stored into other objects that implement the <see cref="IDisposable"/> interface.
    /// </remarks>
    internal sealed class SecretString(char[] contents) : IDisposable
    {
        // Internally, simply store an array of characters
        private readonly char[] data = contents;

        /// <summary>
        /// Gets the total number of characters in this <see cref="SecretString"/>.
        /// </summary>
        public int GetLength()
        {
            return data.Length;
        }

        /// <summary>
        /// Gets a read-only view of the string contents in the form of a <see cref="ReadOnlySpan&lt;char&gt;"/>.
        /// </summary>
        public ReadOnlySpan<char> GetData()
        {
            return data.AsSpan();
        }

        /// <summary>
        /// Gets a modifiable view of the string contents in the form of a <see cref="Span&lt;char&gt;"/>.
        /// </summary>
        public Span<char> GetData_W()
        {
            return data.AsSpan();
        }

        /// <summary>
        /// Checks if the provided <see cref="SecretString"/> object is null or empty.
        /// </summary>
        public static bool IsNullOrEmpty([NotNullWhen(false)] SecretString? str)
        {
            return str == null || str.GetLength() == 0;
        }

        /// <summary>
        /// Checks if the provided <see cref="SecretString"/> object is null, empty,
        ///  or contains only whitespace characters.
        /// </summary>
        public static bool IsNullOrWhiteSpace([NotNullWhen(false)] SecretString? str)
        {
            if (str == null) return true;

            for (int i = 0; i < str.GetLength(); i++)
            {
                if (!char.IsWhiteSpace(str.data[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if the <see cref="SecretString"/> contains an occurrence of any of the specified characters.
        /// </summary>
        public bool ContainsAny(SearchValues<char> searchValues)
        {
            return MemoryExtensions.ContainsAny(data, searchValues);
        }

        /// <summary>
        /// Checks whether this and the specified <see cref="SecretString"/> have the same contents.
        /// </summary>
        public bool ContentEquals(SecretString? other)
        {
            if (other is null)
                return false;

            if (this.GetLength() != other.GetLength())
                return false;

            return MemoryExtensions.CompareTo(this.data, other.data, StringComparison.Ordinal) == 0;
        }

        /// <summary>
        /// Returns the content of this <see cref="SecretString"/> converted into a regular <see cref="string"/>.
        /// </summary>
        /// <remarks>
        /// <b>NOTE:</b> This function is provided only for compatibility with APIs
        ///   that do not take <see cref="Span&lt;char&gt;"/> parameters.
        /// </remarks>
        public override string ToString()
        {
            return new string(data);
        }

        /// <summary>
        /// Wipes the content of this <see cref="SecretString"/> from memory, overwriting it with zeros.
        /// </summary>
        public void Zeroise()
        {
            Span<byte> bytes = MemoryMarshal.AsBytes(data.AsSpan());
            CryptographicOperations.ZeroMemory(bytes);
        }

        public void Dispose()
        {
            Zeroise();
        }
    }
}
