using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace BlazorBlaze.VectorGraphics;

public static class ArrayBufferExtensions
{
    public static ManagedArray<T> ToArrayBuffer<T>(this IReadOnlyList<T> items)
    {
        var buffer = new ManagedArray<T>(items.Count);
        for (var i = 0; i < items.Count; i++) buffer.Add(items[i]);

        return buffer;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ManagedArray<U> ToArrayBuffer<T,U>(this IReadOnlyList<T> items, Func<T,U> conv)
    {
        var buffer = new ManagedArray<U>(items.Count);
        for (var i = 0; i < items.Count; i++) 
            buffer.Add(conv(items[i]));

        return buffer;
    }
    
    public static ManagedArray<T> ToArrayBuffer<T>(this IEnumerable<T> items)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        var count = items.Count();
        var buffer = new ManagedArray<T>(count);
        int i = 0;
        // ReSharper disable once PossibleMultipleEnumeration
        foreach (var j in items) buffer.Add(j);

        return buffer;
    }
    public static ManagedArray<T> ToArrayBuffer<T>(this T[] items)
    {
        var buffer = new ManagedArray<T>(items.Length);
        for (var i = 0; i < items.Length; i++) buffer.Add(items[i]);

        return buffer;
    }
    public static ManagedArray<T> ToArrayBuffer<T>(this IList<T> items)
    {
        var buffer = new ManagedArray<T>(items.Count);
        for (var i = 0; i < items.Count; i++) buffer.Add(items[i]);

        return buffer;
    }
}
