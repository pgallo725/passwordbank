using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace PasswordBank.DataModel
{
    /// <summary>
    /// A class to replace usage of .NET strings when storing short-lived plaintext secrets in memory.<br/>
    /// The implementation controls the underlying string memory in order to wipe its contents
    ///  when the secret gets disposed or is no longer needed.
    /// </summary>
    internal class SecretString(char[] contents) : IEquatable<SecretString>, IDisposable
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
        /// Checks if the provided <see cref="SecretString"/> object is null, empty or contains only whitespace characters.
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

        public override bool Equals(object? obj)
        {
            return Equals(obj as SecretString);
        }

        /// <summary>
        /// Checks whether this and the specified <see cref="SecretString"/> have the same contents.
        /// </summary>
        public bool Equals(SecretString? other)
        {
            if (other is null)
                return false;

            if (this.GetLength() != other.GetLength())
                return false;

            return MemoryExtensions.CompareTo(this.data, other.data, StringComparison.Ordinal) == 0;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current <see cref="SecretString"/>.</returns>
        public override int GetHashCode()
        {
            return data.GetHashCode();
        }

        /// <summary>
        /// Returns a <see cref="string"/> representation of the contents of this <see cref="SecretString"/>.
        /// </summary>
        /// <remarks>
        /// NOTE: Calling this function defeats the purpose of using a <see cref="SecretString"/> in the first place.<br/>
        /// It's provided only for compatibility with third-party APIs that do not offer <see cref="Span&lt;char&gt;"/> alternatives.
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
            Array.Fill(data, '\0');

            // Prevent the JIT from optimizing away the Array.Fill() call above
            GC.KeepAlive(data);
        }

        public void Dispose()
        {
            Zeroise();
        }
    }
}
