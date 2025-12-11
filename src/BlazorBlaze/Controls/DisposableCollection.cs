using System.Collections;

namespace BlazorBlaze;

public class DisposableCollection : IDisposable, IEnumerable
{
    private readonly LinkedList<IDisposable> _index = new();
    private bool _disposed = false;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public DisposableCollection Add(IEnumerable<IDisposable> items)
    {
        foreach (var i in items) Add(i);
        return this;
    }
    public DisposableCollection Add(params IDisposable[] items)
    {
        foreach (var i in items) Add(i);
        return this;
    }
    public DisposableCollection Add(IDisposable item)
    {
        _index.AddLast(item);
        return this;
    }
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            Clear();
        }

        _disposed = true;
    }

    public void Clear()
    {
        foreach (var disposable in _index)
        {
            disposable.Dispose();
        }

        _index.Clear();
    }

    public IEnumerator GetEnumerator()
    {
        return ((IEnumerable)_index).GetEnumerator();
    }
}