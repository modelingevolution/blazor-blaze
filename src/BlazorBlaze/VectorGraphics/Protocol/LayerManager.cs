using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Manages multiple rendering layers with their own backing bitmaps.
/// Layers can be composited onto a final canvas in order.
/// </summary>
public class LayerManager : IDisposable
{
    private readonly int _width;
    private readonly int _height;
    private readonly Dictionary<byte, LayerCanvas> _layers = new();
    private readonly object _sync = new();
    private bool _disposed;

    public LayerManager(int width, int height)
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Gets or creates a layer canvas for the specified layer ID.
    /// </summary>
    public LayerCanvas GetLayer(byte layerId)
    {
        lock (_sync)
        {
            if (!_layers.TryGetValue(layerId, out var layer))
            {
                layer = new LayerCanvas(_width, _height, layerId);
                _layers[layerId] = layer;
            }
            return layer;
        }
    }

    /// <summary>
    /// Gets all layer IDs in sorted order.
    /// </summary>
    public IEnumerable<byte> GetLayerIds()
    {
        lock (_sync)
        {
            return _layers.Keys.OrderBy(x => x).ToArray();
        }
    }

    /// <summary>
    /// Composites all layers onto the target canvas in layer order.
    /// </summary>
    public void Composite(SKCanvas target)
    {
        lock (_sync)
        {
            foreach (var layerId in _layers.Keys.OrderBy(x => x))
            {
                var layer = _layers[layerId];
                layer.DrawTo(target);
            }
        }
    }

    /// <summary>
    /// Clears a specific layer to transparent.
    /// </summary>
    public void ClearLayer(byte layerId)
    {
        lock (_sync)
        {
            if (_layers.TryGetValue(layerId, out var layer))
            {
                layer.Clear();
            }
        }
    }

    /// <summary>
    /// Clears all layers to transparent.
    /// </summary>
    public void ClearAll()
    {
        lock (_sync)
        {
            foreach (var layer in _layers.Values)
            {
                layer.Clear();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_sync)
        {
            foreach (var layer in _layers.Values)
            {
                layer.Dispose();
            }
            _layers.Clear();
        }
    }
}

/// <summary>
/// A single rendering layer backed by an SKBitmap.
/// </summary>
public class LayerCanvas : IDisposable
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;
    private readonly byte _layerId;
    private bool _disposed;

    public byte LayerId => _layerId;

    public LayerCanvas(int width, int height, byte layerId)
    {
        _layerId = layerId;
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _canvas = new SKCanvas(_bitmap);
        Clear();
    }

    /// <summary>
    /// Gets the underlying SKCanvas for drawing operations.
    /// </summary>
    public SKCanvas Canvas => _canvas;

    /// <summary>
    /// Clears the layer to transparent.
    /// </summary>
    public void Clear()
    {
        _canvas.Clear(SKColors.Transparent);
    }

    /// <summary>
    /// Draws this layer onto the target canvas.
    /// </summary>
    public void DrawTo(SKCanvas target)
    {
        target.DrawBitmap(_bitmap, 0, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _canvas.Dispose();
        _bitmap.Dispose();
    }
}
