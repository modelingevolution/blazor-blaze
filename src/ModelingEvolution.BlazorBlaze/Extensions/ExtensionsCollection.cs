using System.Collections;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace ModelingEvolution.BlazorBlaze;

//public interface IExtensionsCollection<TExtension>
//{
//    T Get<T>() where T:TExtension;
//    bool TryGetValue(Type key, out TExtension value);
//    int Count { get; }
//    ICollection<Type> Keys { get; }
//    TExtension this[Type key] { get; set; }
//    bool Contains(Type item);
//}

public class ExtensionsCollection<TExtension> : IEnumerable<TExtension>
{
    class TypeComparer : IComparer<Type>
    {
        public int Compare(Type x, Type y) => string.Compare(x.FullName, y.FullName, StringComparison.Ordinal);
    }
    private readonly SortedList<Type, TExtension> _items = new(new TypeComparer());
    public event EventHandler<TExtension>? OnExtensionAdded;
    public event EventHandler<TExtension>? OnExtensionRemoved;
    private void Add(Type key, TExtension value)
    {
        _items.Add(key, value);
        OnExtensionAdded?.Invoke(this, value);
    }

    public void Clear()
    {
        _items.Clear();
    }
    public bool Contains<TExtension>() => _items.ContainsKey(typeof(TExtension));

    public bool Contains(TExtension value)
    {
        return _items.ContainsValue(value);
    }

    private bool Remove(Type key, out TExtension value)
    {
        if (!_items.Remove(key, out value)) return false;
        
        OnExtensionRemoved?.Invoke(this, value);
        return true;
    }

    public int Count => _items.Count;

    public TExtension this[Type key]
    {
        get => _items[key];
    }

    public bool TryGet<T>(out T value) where T:TExtension
    {
        if (_items.TryGetValue(typeof(T), out var t))
        {
            value = (T)t;
            return true;
        }

        value = default!;
        return false;
    }
    public T GetOrAdd<T>() where T : TExtension
    {
        if (_items.TryGetValue(typeof(T), out var t))
            return (T)t!;
        else
        {
            var instance = Activator.CreateInstance<T>();
            Add(typeof(T), instance);
            return instance;
        }
    }
    public void Enable<T>(Action<T>? onConfigure = null) where T : TExtension
    {
        if (Contains(typeof(T))) return;
        var extension = Activator.CreateInstance<T>();
        Add(typeof(T), extension);
        onConfigure?.Invoke(extension);
    }

    public void Disable<T>() where T : TExtension
    {
        Remove(typeof(T), out var e);
    }
    public bool Disable(TExtension extension) => Remove(extension.GetType(), out var e);
    public void Add(TExtension item) => this.Add(item.GetType(), item);

    public ICollection<Type> Keys => _items.Keys;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool Contains(Type item) => _items.ContainsKey(item);


    public IEnumerator<TExtension> GetEnumerator()
    {
        return _items.Values.GetEnumerator();
    }


    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_items).GetEnumerator();
    }
}