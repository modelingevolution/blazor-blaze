using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Manages multiple rendering layers with their own backing bitmaps.
/// Layers can be composited onto a final canvas in order.
/// Uses SortedDictionary to maintain layer ordering without per-frame sorting.
/// </summary>
public class LayerManager : IDisposable
{
    private readonly int _width;
    private readonly int _height;
    private readonly SortedDictionary<byte, LayerCanvas> _layers = new();
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
    /// Gets all layer IDs in sorted order (SortedDictionary maintains order).
    /// </summary>
    public IEnumerable<byte> GetLayerIds()
    {
        lock (_sync)
        {
            return _layers.Keys.ToArray();
        }
    }

    /// <summary>
    /// Composites all layers onto the target canvas in layer order.
    /// SortedDictionary maintains order, so no per-frame sorting needed.
    /// </summary>
    public void Composite(SKCanvas target)
    {
        lock (_sync)
        {
            foreach (var layer in _layers.Values)
            {
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

public interface ILayer : IDisposable
{
    byte LayerId { get; }
    ICanvas Canvas { get; }
    void Clear();
    void DrawTo(SKCanvas target);

}
/// <summary>
/// A single rendering layer backed by an SKBitmap.
/// </summary>
public class LayerCanvas : ILayer
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _skCanvas;
    private readonly SkiaCanvasAdapter _canvas;
    private readonly byte _layerId;
    private bool _disposed;

    public byte LayerId => _layerId;

    public LayerCanvas(int width, int height, byte layerId)
    {
        _layerId = layerId;
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _skCanvas = new SKCanvas(_bitmap);
        _canvas = new SkiaCanvasAdapter(_skCanvas);
        Clear();
    }

    public ICanvas Canvas => _canvas;

    /// <summary>
    /// Clears the layer to transparent.
    /// </summary>
    public void Clear()
    {
        _skCanvas.Clear(SKColors.Transparent);
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

        _skCanvas.Dispose();
        _bitmap.Dispose();
    }
}

/// <summary>
/// Wraps SKCanvas in Protocol.ICanvas interface.
/// </summary>
internal class SkiaCanvasAdapter : ICanvas
{
    private readonly SKCanvas _canvas;

    [ThreadStatic]
    private static SKPath? _reusablePath;

    public SkiaCanvasAdapter(SKCanvas canvas) => _canvas = canvas;

    public void Save() => _canvas.Save();
    public void Restore() => _canvas.Restore();
    public void SetMatrix(SKMatrix matrix) => _canvas.SetMatrix(matrix);

    public void DrawPolygon(ReadOnlySpan<SKPoint> points, RgbColor stroke, int thickness)
    {
        if (points.Length == 0) return;

        var paint = SKPaintCache.Instance.GetStrokePaint(stroke, (ushort)thickness);
        var path = _reusablePath ??= new SKPath();
        path.Reset();

        path.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++)
            path.LineTo(points[i]);
        path.Close();

        _canvas.DrawPath(path, paint);
    }

    public void DrawText(string text, int x, int y, RgbColor color, int fontSize)
    {
        var (paint, font) = SKPaintCache.Instance.GetTextPaint(color, (ushort)fontSize);
        _canvas.DrawText(text, x, y, font, paint);
    }

    public void DrawCircle(int centerX, int centerY, int radius, RgbColor stroke, int thickness)
    {
        var paint = SKPaintCache.Instance.GetStrokePaint(stroke, (ushort)thickness);
        _canvas.DrawCircle(centerX, centerY, radius, paint);
    }

    public void DrawRect(int x, int y, int width, int height, RgbColor stroke, int thickness)
    {
        var paint = SKPaintCache.Instance.GetStrokePaint(stroke, (ushort)thickness);
        _canvas.DrawRect(x, y, width, height, paint);
    }

    public void DrawLine(int x1, int y1, int x2, int y2, RgbColor stroke, int thickness)
    {
        var paint = SKPaintCache.Instance.GetStrokePaint(stroke, (ushort)thickness);
        _canvas.DrawLine(x1, y1, x2, y2, paint);
    }
}
