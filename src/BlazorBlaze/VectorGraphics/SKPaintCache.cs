using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// Thread-safe cache for SKPaint objects to avoid allocation during rendering.
/// Paints are cached by a composite key of color, style, and stroke width.
/// </summary>
public sealed class SKPaintCache : IDisposable
{
    private readonly ConcurrentDictionary<PaintKey, SKPaint> _strokePaints = new();
    private readonly ConcurrentDictionary<PaintKey, SKPaint> _fillPaints = new();
    private readonly ConcurrentDictionary<TextPaintKey, (SKPaint Paint, SKFont Font)> _textPaints = new();
    private bool _disposed;

    public static SKPaintCache Instance { get; } = new();

    private readonly record struct PaintKey(SKColor Color, ushort StrokeWidth);
    private readonly record struct TextPaintKey(SKColor Color, ushort Size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SKPaint GetStrokePaint(SKColor color, ushort strokeWidth)
    {
        var key = new PaintKey(color, strokeWidth);
        return _strokePaints.GetOrAdd(key, static k => new SKPaint
        {
            Color = k.Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = k.StrokeWidth,
            IsAntialias = true
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SKPaint GetFillPaint(SKColor color)
    {
        var key = new PaintKey(color, 0);
        return _fillPaints.GetOrAdd(key, static k => new SKPaint
        {
            Color = k.Color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (SKPaint Paint, SKFont Font) GetTextPaint(SKColor color, ushort size)
    {
        var key = new TextPaintKey(color, size);
        return _textPaints.GetOrAdd(key, static k =>
        {
            var paint = new SKPaint
            {
                Color = k.Color,
                IsAntialias = true
            };
            var font = new SKFont(SKTypeface.Default, k.Size);
            return (paint, font);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var paint in _strokePaints.Values)
            paint.Dispose();
        _strokePaints.Clear();

        foreach (var paint in _fillPaints.Values)
            paint.Dispose();
        _fillPaints.Clear();

        foreach (var (paint, font) in _textPaints.Values)
        {
            paint.Dispose();
            font.Dispose();
        }
        _textPaints.Clear();
    }
}
