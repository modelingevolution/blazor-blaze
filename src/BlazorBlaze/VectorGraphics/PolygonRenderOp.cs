using System.Buffers;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// Buffered polygon rendering operation for deferred drawing.
/// Uses ArrayPool to avoid per-polygon allocations.
/// </summary>
internal class PolygonRenderOp : IRenderOp, IDisposable
{
    private SKPoint[] _points;
    private readonly int _pointCount;
    private readonly RgbColor _color;
    private readonly ushort _width;
    private bool _ownsArray;

    /// <summary>
    /// Creates a PolygonRenderOp that takes ownership of a pooled array.
    /// </summary>
    /// <param name="points">Pooled array from ArrayPool</param>
    /// <param name="pointCount">Actual number of points in the array</param>
    /// <param name="color">Stroke color</param>
    /// <param name="width">Stroke width</param>
    /// <param name="ownsArray">If true, array will be returned to pool on dispose</param>
    public PolygonRenderOp(SKPoint[] points, int pointCount, RgbColor color, ushort width, bool ownsArray = true)
    {
        _points = points;
        _pointCount = pointCount;
        _color = color;
        _width = width;
        _ownsArray = ownsArray;
    }

    public ushort Id => 0;

    public void Render(ICanvas canvas)
    {
        if (canvas is SkiaCanvas skiaCanvas)
        {
            skiaCanvas.RenderPolygon(_points.AsSpan(0, _pointCount), _color, _width);
        }
    }

    public void Dispose()
    {
        if (_ownsArray && _points != null)
        {
            ArrayPool<SKPoint>.Shared.Return(_points);
            _points = null!;
            _ownsArray = false;
        }
    }
}
