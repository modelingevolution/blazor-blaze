using BlazorBlaze.VectorGraphics;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace BlazorBlaze.ValueTypes;

public struct SpinLock
{
    private int _locked; // 0 = unlocked, 1 = locked
    
    public void Enter()
    {
        while (Interlocked.CompareExchange(ref _locked, 1, 0) != 0)
        {
            Thread.SpinWait(10);
        }
    }

    public void Exit()
    {
        Interlocked.Exchange(ref _locked, 0);
    }
}

/// <summary>
/// Immutable array of ref-counted items.
///
/// DESIGN DECISIONS - DO NOT CHANGE WITHOUT AUTHORIZATION:
///
/// 1. THIS IS A STRUCT BY DESIGN - NOT A CLASS!
///    - You CANNOT assign RefArray to a temporary variable
///    - You CANNOT pass it by value casually
///    - Struct copy = broken ref counting. This is INTENTIONAL to force correct usage.
///
/// 2. USE TryCopy() TO GET A NEW INSTANCE
///    - TryCopy increments ref counts on all Ref&lt;T&gt; items
///    - Returns a new RefArray pointing to the same ImmutableArray
///    - This is the ONLY correct way to "copy" a RefArray
///
/// 3. SpinLock IS PER-INSTANCE BY DESIGN
///    - Each RefArray instance has its own SpinLock
///    - This is fine because Ref&lt;T&gt; handles its own thread-safety via Interlocked
///    - The SpinLock protects the _disposed flag and iteration within ONE instance
///
/// 4. DO NOT CHANGE TO CLASS - it breaks the design that prevents casual copying
///
/// 5. DO NOT ADD ROLLBACK LOGIC - keep it simple
///
/// 6. DO NOT COPY THE ARRAY IN TryCopy - just increment ref counts
/// </summary>
public struct RefArray<T> : IDisposable where T : IDisposable
{
    private readonly ImmutableArray<Ref<T>?> _array;
    private SpinLock _lock;
    private bool _disposed;
    public RefArray(int capacity)
    {
        _array = ImmutableArray.CreateBuilder<Ref<T>?>(capacity).ToImmutableArray();
        _lock = default;
        _disposed = false;

    }
    internal RefArray(ImmutableArray<Ref<T>?> items)
    {
        _array = items;
        _lock = default;
        _disposed = false;
    }

    /// <summary>
    /// Get the underlying immutable array.
    /// </summary>
    internal ImmutableArray<Ref<T>?> Value => _array;

    /// <summary>
    /// Get value at index. Returns null/default if array is default, index out of range, or slot is null.
    /// </summary>
    public T? this[int index]
    {
        get
        {
            if (_array.IsDefault || index >= _array.Length)
                return default;
            var r = _array[index];
            return r == null ? default : r.Value;
        }
    }

    /// <summary>
    /// Get the underlying Ref at index. Returns null if array is default or index out of range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ref<T>? GetRef(int index) => _array.IsDefault || index >= _array.Length ? null : _array[index];

    /// <summary>
    /// Number of items in the array.
    /// </summary>
    public int Length => _array.IsDefault ? 0 : _array.Length;

    /// <summary>
    /// Atomic copy - increments ref count on all items.
    /// </summary>
    public bool TryCopy(out RefArray<T>? copy)
    {
        _lock.Enter();
        try
        {
            if (_disposed || _array.IsDefault)
            {
                copy = null;
                return false;
            }

            for (int i = 0; i < _array.Length; i++)
                _array[i]?.TryCopy(out _);

            copy = new RefArray<T>(_array);
            return true;
        }
        finally
        {
            _lock.Exit();
        }
    }

    public void Dispose()
    {
        _lock.Enter();
        try
        {
            if (_disposed || _array.IsDefault) return;
            _disposed = true;

            for (int i = 0; i < _array.Length; i++)
            {
                _array[i]?.Dispose();
            }
        }
        finally
        {
            _lock.Exit();
        }
    }
}
