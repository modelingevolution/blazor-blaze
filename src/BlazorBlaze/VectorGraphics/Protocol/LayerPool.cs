using System.Collections.Concurrent;
using BlazorBlaze.ValueTypes;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Thread-safe pool of LayerCanvas instances.
/// Recycles layers to avoid allocation overhead during streaming.
/// </summary>
public sealed class LayerPool : ILayerPool, IDisposable
{
    private readonly int _width;
    private readonly int _height;
    private readonly ConcurrentBag<LayerCanvas> _available = new();
    private readonly ConcurrentBag<LayerCanvas> _all = new();
    private bool _disposed;

    private int _inUseCount;
    private int _totalCreated;

    public LayerPool(int width, int height)
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Number of layers currently rented out (in use).
    /// </summary>
    public int InUseCount => Volatile.Read(ref _inUseCount);

    /// <summary>
    /// Number of layers available in the pool cache.
    /// </summary>
    public int CachedCount => _available.Count;

    /// <summary>
    /// Total number of layers created by this pool.
    /// </summary>
    public int TotalCreated => Volatile.Read(ref _totalCreated);

    /// <summary>
    /// Rent a layer from the pool. Creates new if none available.
    /// The returned Lease will return the layer to the pool when disposed.
    /// </summary>
    public Lease<ILayer> Rent(byte layerId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LayerPool));

        LayerCanvas layer;
        if (_available.TryTake(out var pooled))
        {
            layer = pooled;
            layer.Clear();
        }
        else
        {
            layer = new LayerCanvas(_width, _height, layerId);
            _all.Add(layer);
            Interlocked.Increment(ref _totalCreated);
        }

        Interlocked.Increment(ref _inUseCount);
        return new Lease<ILayer>(layer, Return);
    }

    private void Return(ILayer layer)
    {
        Interlocked.Decrement(ref _inUseCount);

        if (_disposed)
        {
            layer.Dispose();
            return;
        }

        if (layer is LayerCanvas canvas)
        {
            _available.Add(canvas);
        }
        else
        {
            layer.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var layer in _all)
        {
            layer.Dispose();
        }
    }
}
