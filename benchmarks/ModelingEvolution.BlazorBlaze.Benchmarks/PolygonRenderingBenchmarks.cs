using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ModelingEvolution.BlazorBlaze.Collections;
using ModelingEvolution.BlazorBlaze.VectorGraphics;
using ModelingEvolution.BlazorBlaze.VectorGraphics.Protocol;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class PolygonRenderingBenchmarks
{
    private SKSurface _surface = null!;
    private SKCanvas _canvas = null!;
    private SkiaCanvas _skiaCanvas = null!;

    // Pre-allocated data for optimized version
    private ManagedArray<SKPoint>[] _managedPoints = null!;
    private Draw<Polygon>[] _drawOps = null!;

    // Array-based data for non-optimized version
    private SKPoint[][] _arrayPoints = null!;

    private const int PolygonCount = 100;
    private const int PointsPerPolygon = 200;

    private static readonly RgbColor[] Colors =
    [
        new RgbColor(255, 100, 100),
        new RgbColor(100, 255, 100),
        new RgbColor(100, 100, 255),
        new RgbColor(255, 255, 100),
    ];

    [GlobalSetup]
    public void Setup()
    {
        _surface = SKSurface.Create(new SKImageInfo(1200, 800));
        _canvas = _surface.Canvas;
        _skiaCanvas = new SkiaCanvas();

        // Setup ManagedArray version (optimized)
        _managedPoints = new ManagedArray<SKPoint>[PolygonCount];
        _drawOps = new Draw<Polygon>[PolygonCount];

        for (int i = 0; i < PolygonCount; i++)
        {
            _managedPoints[i] = new ManagedArray<SKPoint>(PointsPerPolygon);
            for (int j = 0; j < PointsPerPolygon; j++)
            {
                double angle = 2 * Math.PI * j / PointsPerPolygon;
                float x = (float)(600 + 100 * Math.Cos(angle));
                float y = (float)(400 + 100 * Math.Sin(angle));
                _managedPoints[i].Add(new SKPoint(x, y));
            }

            _drawOps[i] = new Draw<Polygon>
            {
                Value = new Polygon(_managedPoints[i]),
                Context = new DrawContext { Stroke = Colors[i % Colors.Length], Thickness = 1 }
            };
        }

        // Setup array version (non-optimized)
        _arrayPoints = new SKPoint[PolygonCount][];
        for (int i = 0; i < PolygonCount; i++)
        {
            _arrayPoints[i] = new SKPoint[PointsPerPolygon];
            for (int j = 0; j < PointsPerPolygon; j++)
            {
                double angle = 2 * Math.PI * j / PointsPerPolygon;
                float x = (float)(600 + 100 * Math.Cos(angle));
                float y = (float)(400 + 100 * Math.Sin(angle));
                _arrayPoints[i][j] = new SKPoint(x, y);
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _surface?.Dispose();

        if (_drawOps != null)
        {
            for (int i = 0; i < _drawOps.Length; i++)
            {
                _drawOps[i].Value?.Dispose();
            }
        }
    }

    [Benchmark(Baseline = true)]
    public void RenderWithAllocations()
    {
        _skiaCanvas.Begin(1, 0);

        for (int i = 0; i < PolygonCount; i++)
        {
            var color = Colors[i % Colors.Length];
            // This allocates new Polygon and ManagedArray per frame
            _skiaCanvas.Add(new Draw<Polygon>
            {
                Value = new Polygon(_arrayPoints[i]),
                Context = new DrawContext { Stroke = color, Thickness = 1 }
            }, 0);
        }

        _skiaCanvas.End(0);
        _skiaCanvas.Render(_canvas);
    }

    [Benchmark]
    public void RenderWithReuse()
    {
        _skiaCanvas.Begin(1, 0);

        // Reuse pre-allocated objects - zero allocation per frame
        for (int i = 0; i < PolygonCount; i++)
        {
            _skiaCanvas.Add(_drawOps[i], 0);
        }

        _skiaCanvas.End(0);
        _skiaCanvas.Render(_canvas);
    }
}
