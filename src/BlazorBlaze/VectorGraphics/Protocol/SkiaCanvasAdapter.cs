using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

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

    public void DrawJpeg(in ReadOnlySpan<byte> jpegData, int x, int y, int width, int height)
    {
        using var image = SKImage.FromEncodedData(jpegData);
        if (image == null) return;

        var destRect = new SKRect(x, y, x + width, y + height);
        _canvas.DrawImage(image, destRect);
    }
}
