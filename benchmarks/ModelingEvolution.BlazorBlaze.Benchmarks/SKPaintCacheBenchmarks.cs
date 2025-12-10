using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ModelingEvolution.BlazorBlaze.VectorGraphics;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class SKPaintCacheBenchmarks
{
    private SKSurface _surface = null!;
    private SKCanvas _canvas = null!;
    private SKPath _path = null!;

    private static readonly SKColor[] TestColors =
    [
        SKColors.Red,
        SKColors.Green,
        SKColors.Blue,
        SKColors.Yellow,
        SKColors.Cyan,
        SKColors.Magenta,
        SKColors.Orange,
        SKColors.Purple
    ];

    [GlobalSetup]
    public void Setup()
    {
        _surface = SKSurface.Create(new SKImageInfo(1200, 800));
        _canvas = _surface.Canvas;

        // Create a sample path
        _path = new SKPath();
        _path.MoveTo(100, 100);
        for (int i = 0; i < 100; i++)
        {
            double angle = 2 * Math.PI * i / 100;
            _path.LineTo(
                (float)(600 + 200 * Math.Cos(angle)),
                (float)(400 + 200 * Math.Sin(angle)));
        }
        _path.Close();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _path?.Dispose();
        _surface?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void DrawWithNewPaint()
    {
        for (int i = 0; i < 100; i++)
        {
            var color = TestColors[i % TestColors.Length];
            using var paint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (ushort)(1 + i % 5),
                IsAntialias = true
            };
            _canvas.DrawPath(_path, paint);
        }
    }

    [Benchmark]
    public void DrawWithCachedPaint()
    {
        for (int i = 0; i < 100; i++)
        {
            var color = TestColors[i % TestColors.Length];
            var paint = SKPaintCache.Instance.GetStrokePaint(color, (ushort)(1 + i % 5));
            _canvas.DrawPath(_path, paint);
        }
    }

    [Benchmark]
    public void TextWithNewPaintAndFont()
    {
        for (int i = 0; i < 100; i++)
        {
            var color = TestColors[i % TestColors.Length];
            using var paint = new SKPaint
            {
                Color = color,
                IsAntialias = true
            };
            using var font = new SKFont(SKTypeface.Default, 16);
            _canvas.DrawText("Hello World", 100, 100, font, paint);
        }
    }

    [Benchmark]
    public void TextWithCachedPaintAndFont()
    {
        for (int i = 0; i < 100; i++)
        {
            var color = TestColors[i % TestColors.Length];
            var (paint, font) = SKPaintCache.Instance.GetTextPaint(color, 16);
            _canvas.DrawText("Hello World", 100, 100, font, paint);
        }
    }
}
