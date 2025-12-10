using System.Diagnostics;
using System.Drawing;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze;

public class HitMap
{
    private SKBitmap _bitmap;
    private SKCanvas _canvas;
    private readonly Dictionary<uint, Control> _index = new();

    public HitMap(int width, int height)
    {
        _bitmap = new SKBitmap(width, height);
        
        _canvas = new SKCanvas(_bitmap);
    }

    public void Clear()
    {
        _canvas.Clear(SKColors.Transparent);
    }

    internal void Flush() => _canvas.Flush();
    
    public void Render(Control control)
    {
        var id = control.Id;
        
        //_index.TryAdd(id, control);
        // perform add or update.
        if (_index.TryGetValue(id, out var c))
        {
            if (c != control)
                _index[id] = control;
        } else _index.Add(id, control);
        
        var color = id.ToSKColor();
        Debug.Assert(color.ToUInt32() == id);
        
        using var paint = new SKPaint { Color = color, Style = SKPaintStyle.StrokeAndFill,StrokeWidth = 1};
        control.RenderForHitMap(_canvas, paint);
        
    }

    public Dictionary<uint, int> ComputeArea()
    {
        Dictionary<uint, int> tmp = new Dictionary<uint, int>();
        var pixels = _bitmap.Pixels;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] != SKColors.Transparent.ToUInt32())
            {
                var k = pixels[i].ToUInt32();
                tmp.TryAdd(k, 0);
                tmp[k]++;
            }
        }

        return tmp;
    }
    public Control? GetControlAt(SKPoint point)
    {
        
        var color = _bitmap.GetPixel((int)point.X, (int)point.Y);
        //Console.WriteLine($"[{point.X} {point.Y}] {color.ToString()}");
        //foreach(var i in ComputeArea())
        //    Console.WriteLine($"{i.Key}: {i.Value}");
        var id = color.ToUInt32();
        return _index.GetValueOrDefault(id);
    }

    public void SetMatrix(SKMatrix matrix)
    {
        this._canvas.SetMatrix(matrix);
    }

    public void Resize(Size size)
    {
        var t1 = _bitmap;
        var t2 = _canvas;
        
        _bitmap = new SKBitmap(size.Width, size.Height);
        _canvas = new SKCanvas(_bitmap);
        
        t2.Dispose();
        t1.Dispose();
    }

    public void Save() => _canvas.Save();

    public void Translate(SKPoint controlAbsoluteOffset) => _canvas.Translate(controlAbsoluteOffset);

    public void Restore() => _canvas.Restore();
}