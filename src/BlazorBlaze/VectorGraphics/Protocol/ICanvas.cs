using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Canvas interface that mirrors SKCanvas API.
/// Implementations wrap a layer's SKCanvas and provide drawing operations.
/// </summary>
public interface ICanvas
{
    // Canvas state operations (mirror SKCanvas)
    void Save();
    void Restore();
    void SetMatrix(SKMatrix matrix);

    // Draw operations
    void DrawPolygon(ReadOnlySpan<SKPoint> points, RgbColor stroke, int thickness);
    void DrawText(string text, int x, int y, RgbColor color, int fontSize);
    void DrawCircle(int centerX, int centerY, int radius, RgbColor stroke, int thickness);
    void DrawRect(int x, int y, int width, int height, RgbColor stroke, int thickness);
    void DrawLine(int x1, int y1, int x2, int y2, RgbColor stroke, int thickness);
}
