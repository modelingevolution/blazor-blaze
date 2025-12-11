using System.Buffers;
using System.Collections.Concurrent;
using SkiaSharp;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace BlazorBlaze.VectorGraphics;

public class SkiaCanvas : ICanvas
{
    class Layer
    {
        public List<IRenderOp> RenderBuffer = new();
        public List<IRenderOp> OpSink = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Swap()
        {
            var temp = RenderBuffer;
            RenderBuffer = OpSink;
            for (int i = 0; i < temp.Count; i++)
            {
                if (temp[i] is IDisposable d) d.Dispose();
            }
            temp.Clear();
            OpSink = temp;
        }
    }

    private readonly Layer[] _layers = new Layer[255];
    private volatile byte[] _layerIx = Array.Empty<byte>();
    private readonly object _sync = new();
    private byte[] LayerIx => _layerIx;
    

    private Layer GetLayer(byte ix)
    {
        if (_layers[ix] != null!)
            return _layers[ix];
        lock (_sync)
        {
            if (_layers[ix] != null!)
                return _layers[ix];
            else
            {
                var l = new Layer();
                _layers[ix] = l;
                var n = new byte[_layerIx.Length + 1];
                Array.Copy(_layerIx, n,_layerIx.Length);
                n[_layerIx.Length] = ix;
                Array.Sort(n);
                _layerIx = n;
                return l;
            }
        }
        
    }


    private readonly PeriodicConsoleWriter _writer = new(TimeSpan.FromSeconds(30));
    private SKCanvas _canvas;
    private byte DefaultLayerId { get; set; }
    public void Add(IRenderOp op, byte? layerId) => GetLayer(layerId ?? DefaultLayerId).OpSink.Add(op);



    public void Render(SKCanvas canvas)
    {
        _canvas = canvas;
        var ls = LayerIx;
        for (byte il = 0; il < ls.Length; il++)
        {
            var layer = GetLayer(il);
            var ops = layer.RenderBuffer;
            for (var index = 0; index < ops.Count; index++)
            {
                var i = ops[index];
                i.Render(this);
            }
        }
    }
    public void End(byte? layerId)
    {
        GetLayer(layerId ?? DefaultLayerId).Swap();
    }

    public void Begin(ulong frameNr, byte? layerId)
    {
        _writer.WriteLine($"Render frame: {frameNr}");
    }

    

    public void DrawRectangle(System.Drawing.Rectangle rect, RgbColor? color, byte? layerId)
    {
        throw new NotImplementedException();
    }


    public void DrawText(string text, int x, int y, int size, RgbColor? color, byte? layerId=null)
    {
        var (paint, font) = SKPaintCache.Instance.GetTextPaint(color ?? RgbColor.Black, (ushort)size);
        _canvas.DrawText(text, x, y, font, paint);
    }

    public byte LayerId { get; set; }

    public object Sync { get; } = new object();

    public void DrawPolygon(ReadOnlySpan<SKPoint> points, RgbColor? color = null, int width = 1, byte? layerId = null)
    {
        // Rent array from pool - PolygonRenderOp will return it on dispose
        var pooledArray = ArrayPool<SKPoint>.Shared.Rent(points.Length);
        points.CopyTo(pooledArray);
        Add(new PolygonRenderOp(pooledArray, points.Length, color ?? RgbColor.Black, (ushort)width, ownsArray: true), layerId);
    }

    // Thread-local SKPath for reuse - avoids native allocation per polygon
    [ThreadStatic]
    private static SKPath? _reusablePath;

    internal void RenderPolygon(ReadOnlySpan<SKPoint> points, RgbColor color, ushort width)
    {
        var paint = SKPaintCache.Instance.GetStrokePaint(color, width);

        // Reuse SKPath instead of allocating new one each time
        var path = _reusablePath ??= new SKPath();
        path.Reset();

        if (points.Length > 0)
        {
            path.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++)
            {
                path.LineTo(points[i]);
            }
            path.Close();
        }

        _canvas.DrawPath(path, paint);
    }
}