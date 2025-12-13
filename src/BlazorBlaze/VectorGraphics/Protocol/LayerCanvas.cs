using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

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
