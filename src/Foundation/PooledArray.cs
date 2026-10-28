using System.Buffers;

namespace PasswordBank.Foundation
{
    /// <summary>
    /// A simple value type that wraps a pool-allocated array with two main goals:
    /// <list type="number">
    ///   <item>Provide a RAII-like mechanism for automatically renting/returning buffers from <see cref="ArrayPool&lt;T&gt;"/>,
    ///     enabled by the <see cref="IDisposable"/> interface and the <see langword="using"/> keyword.</item>
    ///   <item>Restrict access to the valid portion of pool-allocated arrays via <see cref="Span&lt;T&gt;"/>,
    ///     hiding the implementation detail that such buffers could be larger than the requested size.</item>
    /// </list>
    /// </summary>
    /// <typeparam name="T">The type of elements in the array, must be an integral or POD type.</typeparam>
    /// <param name="size">The number of elements to allocate in this <see cref="PooledArray&lt;T&gt;"/>.</param>
    internal readonly ref struct PooledArray<T>(int size) : IDisposable
        where T : unmanaged
    {
        /// <summary>
        /// The pool-allocated buffer backing the <see cref="PooledArray&lt;T&gt;"/>.<br/>
        /// </summary>
        /// <remarks> NOTE: This might be larger than the requested array size.<br/>
        /// Prefer using <see cref="GetData"/> and <see cref="GetData_W"/> whenever possible.
        /// </remarks>
        public T[] Buffer { get; } = ArrayPool<T>.Shared.Rent(size);

        /// <summary>
        /// The effective number of elements in this <see cref="PooledArray&lt;T&gt;"/>. 
        /// </summary>
        public int Length { get; } = size;


        /// <summary>
        /// Gets a read-only view of the array contents in the form of a <see cref="ReadOnlySpan&lt;T&gt;"/>.
        /// </summary>
        public ReadOnlySpan<T> GetData()
        {
            return Buffer.AsSpan(0, Length);
        }

        /// <summary>
        /// Gets a modifiable view of the array contents in the form of a <see cref="Span&lt;T&gt;"/>.
        /// </summary>
        public Span<T> GetData_W()
        {
            return Buffer.AsSpan(0, Length);
        }

        /// <summary>
        /// Clears the contents of this <see cref="PooledArray&lt;T&gt;"/>.
        /// </summary>
        public void Clear()
        {
            Buffer.AsSpan(0, Length).Clear();
        }

        /// <summary>
        /// Returns the allocated buffer to the pool, after clearing its contents.
        /// </summary>
        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(Buffer, clearArray: true);
        }
    }
}
