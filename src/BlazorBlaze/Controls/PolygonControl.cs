using ModelingEvolution.Drawing;
using SkiaSharp;

namespace BlazorBlaze;

public class PolygonControl : ShapeControl
{
    public Polygon<float> Polygon
    {
        get => _polygon;
        set
        {
            _polygon = value;
            _path.Dispose();
            RefreshPolygon();
            //_path2 = ConvertPolygonToSKPath(value);
        }
    }

    public void RefreshPolygon()
    {
        _path = _polygon.ToSKPath();
    }

    private SKPath _path;
    private Polygon<float> _polygon;

    public PolygonControl(Polygon<float> polygon)
    {
        _path = polygon.ToSKPath();
        _polygon = polygon;
    }

    
    public override void Render(SKCanvas canvas, SKRect viewport)
    {
        if (this.PaintStyle != SKPaintStyle.Stroke)
        {
            using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = Fill };
            canvas.DrawPath(_path, fill);
        }

        if (this.PaintStyle != SKPaintStyle.Fill)
        {

            using var stroke = new SKPaint
                { Style = SKPaintStyle.Stroke, Color = Stroke, IsStroke = true, StrokeWidth = StrokeWidth };
            canvas.DrawPath(_path, stroke);
        }
    }
    public override void RenderForHitMap(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawPath(_path, paint);
    }

    protected override void Dispose(bool disposing)
    {
        _path.Dispose();
        base.Dispose(disposing);
    }

  
}