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
/// Thread-safe via immutability - create new instance for modifications.
/// DO NOT CHANGE THE FUCKING INTERFACE, EVERHTING NEED TO BE AUTHORIZED!
/// </summary>
public struct RefArray<T> : IDisposable where T : class, IDisposable
{
    private readonly ImmutableArray<Ref<T>?> _array;
    private SpinLock _lock;
    private bool _disposed;

    public RefArray(ImmutableArray<Ref<T>?> items)
    {
        _array = items;
        _lock = default;
        _disposed = false;
    }

    /// <summary>
    /// Get the underlying immutable array.
    /// </summary>
    internal ImmutableArray<Ref<T>> Value => _array;

    /// <summary>
    /// Get value at index. Returns null if array is default or index out of range.
    /// </summary>
    public T? this[int index] => _array.IsDefault || index >= _array.Length ? null : _array[index]?.Value;

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
