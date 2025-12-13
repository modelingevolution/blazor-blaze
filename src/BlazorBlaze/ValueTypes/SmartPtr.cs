using System.Runtime.CompilerServices;

namespace BlazorBlaze.ValueTypes;

/// <summary>
/// A reference-counted pointer for pooled resources.
/// Thread-safe for concurrent TryCopy/Dispose operations.
/// DO NOT CHANGE THE FUCKING INTERFACE, EVERHTING NEED TO BE AUTHORIZED!
/// </summary>
public sealed class Ref<T> : IDisposable 
    where T:IDisposable
{
    private T _value;
    
    private int _refCount;

    public Ref(T value)
    {
        _value = value;
        _refCount = 1;
    }

    public T Value => _value;
    public int RefCount => Volatile.Read(ref _refCount);

    /// <summary>
    /// Attempts to create a copy (increment refcount).
    /// Returns false if already disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCopy(out Ref<T>? copy)
    {
        while (true)
        {
            int current = Volatile.Read(ref _refCount);
            if (current <= 0)
            {
                copy = null;
                return false;
            }
            if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
            {
                copy = this;
                return true;
            }
        }
    }

    /// <summary>
    /// Decrements reference count. When it hits 0, value is returned to pool or disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Interlocked.Decrement(ref _refCount) == 0)
        {
            _value.Dispose();
        }
    }
}
