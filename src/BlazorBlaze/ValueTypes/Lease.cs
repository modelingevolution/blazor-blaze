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
public sealed class Lease<T> : IDisposable where T : class
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

/// <summary>
/// A lease for a pooled resource with static pool type for zero-allocation return.
/// The pool type must implement static Return method.
///
/// Usage:
/// - Define pool: class MyPool : IPool&lt;MyItem&gt; { static void Return(MyItem item) {...} }
/// - Rent: new Lease&lt;MyPool, MyItem&gt;(item)
/// - On dispose, calls MyPool.Return(item) statically
/// </summary>
public sealed class Lease<TPool, T> : IDisposable
    where T : class
    where TPool : IPool<T>
{
    private readonly T _value;
    private int _disposed;

    public Lease(T value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
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
    /// Returns the item to the pool via static TPool.Return().
    /// Idempotent - safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            TPool.Return(_value);
        }
    }
}

/// <summary>
/// Interface for pools that can receive returned items.
/// Implement with static abstract for zero-allocation Lease&lt;TPool, T&gt;.
/// </summary>
public interface IPool<T> where T : class
{
    /// <summary>
    /// Returns an item to the pool.
    /// </summary>
    static abstract void Return(T item);
}
