using System.Runtime.CompilerServices;

namespace BlazorBlaze.ValueTypes;

/// <summary>
/// A lease for a pooled resource. On Dispose, returns the item to the pool
/// instead of destroying it.
///
/// Usage:
/// - Pool.Rent() returns Lease&lt;T&gt;
/// - Wrap in Ref&lt;Lease&lt;T&gt;&gt; for reference counting
/// - When all refs released, Lease.Dispose() returns item to pool
/// </summary>
public struct Lease<T> : IDisposable 
{
    private readonly T _value;
    private readonly Action<T> _returnToPool;
    private int _disposed;

    public Lease(T value, Action<T> returnToPool)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
        _returnToPool = returnToPool ?? throw new ArgumentNullException(nameof(returnToPool));
    }

    /// <summary>
    /// The leased value. Do not access after Dispose.
    /// </summary>
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>
    /// Whether this lease has been disposed (returned to pool).
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Returns the item to the pool. Idempotent - safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _returnToPool(_value);
        }
    }
}


