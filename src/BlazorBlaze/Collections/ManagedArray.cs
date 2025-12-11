using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace BlazorBlaze;

/// <summary>
/// Struct-based implementation of a managed array using ArrayPool for memory management.
/// Allows value-type semantics with controlled memory allocation.
/// Performance-critical: avoids GC pressure during frequent tree traversals.
/// </summary>
/// <typeparam name="T">The type of elements in the array</typeparam>
public struct ManagedArrayStruct<T> : ICollection<T>, IList<T>, IReadOnlyList<T>, IDisposable
{
    private T[] _buffer;
    private int _size;
    private bool _disposed;

    public ManagedArrayStruct(int initialCapacity = 0)
    {
        _buffer = initialCapacity > 0
            ? ArrayPool<T>.Shared.Rent(initialCapacity)
            : Array.Empty<T>();
        _size = 0;
        _disposed = false;

        if (_buffer.Length > 0)
        {
            Interlocked.Add(ref ManagedArrayStruct<T>.ALLOCATED_BYTES, _buffer.Length);
        }
    }

    public ManagedArrayStruct(params T[] array)
    {
        _buffer = ArrayPool<T>.Shared.Rent(array.Length);
        Interlocked.Add(ref ManagedArrayStruct<T>.ALLOCATED_BYTES, _buffer.Length);
        Array.Copy(array, 0, _buffer, 0, array.Length);
        _size = array.Length;
        _disposed = false;
    }

    public T this[int index]
    {
        get
        {
            if (index < 0)
                index = _size - index;
            EnsureCapacity(index);
            return _buffer[index];
        }
        set
        {
            EnsureCapacity(index + 1);
            _buffer[index] = value;
            if (index >= _size)
                _size = index + 1;
        }
    }

    public int Count => _size;
    public bool IsReadOnly => false;
    public int Capacity => _buffer.Length;

    public void Add(T item)
    {
        EnsureCapacity(_size + 1);
        _buffer[_size++] = item;
    }

    public void Clear() => Clear(false);

    public void Clear(bool disposeMemory)
    {
        if (_disposed) return;

        if (_buffer.Length > 0 && disposeMemory)
        {
            ArrayPool<T>.Shared.Return(_buffer);
            Interlocked.Add(ref ManagedArrayStruct<T>.ALLOCATED_BYTES, -_buffer.Length);
            _buffer = Array.Empty<T>();
        }
        _size = 0;
    }

    public bool Contains(T item) => IndexOf(item) != -1;

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex + _size > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        Array.Copy(_buffer, 0, array, arrayIndex, _size);
    }

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int IndexOf(T item)
    {
        for (int i = 0; i < _size; i++)
            if (EqualityComparer<T>.Default.Equals(_buffer[i], item))
                return i;
        return -1;
    }

    public void Insert(int index, T item)
    {
        if (index < 0 || index > _size)
            throw new ArgumentOutOfRangeException(nameof(index));

        EnsureCapacity(_size + 1);
        Array.Copy(_buffer, index, _buffer, index + 1, _size - index);
        _buffer[index] = item;
        _size++;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _size)
            throw new ArgumentOutOfRangeException(nameof(index));

        Array.Copy(_buffer, index + 1, _buffer, index, _size - index - 1);
        _buffer[--_size] = default!;
    }

    public Enumerator GetEnumerator() => new Enumerator(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureCapacity(int minCapacity)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ManagedArrayStruct<T>));
        if (minCapacity <= _buffer.Length) return;

        int newCapacity = Math.Max(minCapacity, _size * 2);
        T[] newBuffer = ArrayPool<T>.Shared.Rent(newCapacity);
        Interlocked.Add(ref ManagedArrayStruct<T>.ALLOCATED_BYTES, newBuffer.Length);

        Array.Copy(_buffer, 0, newBuffer, 0, _size);

        if (_buffer.Length > 0)
        {
            ArrayPool<T>.Shared.Return(_buffer);
            Interlocked.Add(ref ManagedArrayStruct<T>.ALLOCATED_BYTES, -_buffer.Length);
        }

        _buffer = newBuffer;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_buffer.Length > 0)
        {
            ArrayPool<T>.Shared.Return(_buffer);
            Interlocked.Add(ref ManagedArrayStruct<T>.ALLOCATED_BYTES, -_buffer.Length);
        }
        _buffer = Array.Empty<T>();
        _size = 0;
        _disposed = true;
    }

    public static long ALLOCATED_BYTES;
    public T[] GetBuffer() => _buffer;

    // Custom struct enumerator for performance
    public struct Enumerator : IEnumerator<T>
    {
        private readonly ManagedArrayStruct<T> _array;
        private int _index;

        internal Enumerator(ManagedArrayStruct<T> array)
        {
            _array = array;
            _index = -1;
        }

        public T Current => _array._buffer[_index];
        object IEnumerator.Current => Current!;

        public bool MoveNext() => ++_index < _array._size;
        public void Reset() => _index = -1;
        public void Dispose() { }
    }
}

public class ManagedArray<T> : ICollection<T>, IList<T>, IReadOnlyList<T>, IDisposable
{
    private ManagedArrayStruct<T> _array;

    public ManagedArrayStruct<T> Array => _array;

    public ManagedArray()
    {
        this._array = new ManagedArrayStruct<T>(0);
    }

    public ManagedArray(int initialCapacity)
    {
        this._array = new ManagedArrayStruct<T>(initialCapacity);
    }

    public ManagedArray(params T[] array)
    {
        this._array = new ManagedArrayStruct<T>(array);
    }


    public T this[int index]
    {
        get => _array[index];
        set => _array[index] = value;
    }

    public int Count => _array.Count;

    public bool IsReadOnly => _array.IsReadOnly;

    public void Add(T item)
    {
        _array.Add(item);
    }

    public void Clear()
    {
        _array.Clear();
    }

    public void Clear(bool disposeMemory)
    {
        _array.Clear(disposeMemory);
    }

    public bool Contains(T item)
    {
        return _array.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _array.CopyTo(array, arrayIndex);
    }

    public bool Remove(T item)
    {
        return _array.Remove(item);
    }

    public int IndexOf(T item)
    {
        return _array.IndexOf(item);
    }

    public void Insert(int index, T item)
    {
        _array.Insert(index, item);
    }

    public void RemoveAt(int index)
    {

        _array.RemoveAt(index);
    }



    public void Dispose()
    {
        _array.Dispose();
    }

    public T[] GetBuffer()
    {
        return _array.GetBuffer();
    }

    public ManagedArrayStruct<T>.Enumerator GetEnumerator() => new ManagedArrayStruct<T>.Enumerator(this.Array);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
