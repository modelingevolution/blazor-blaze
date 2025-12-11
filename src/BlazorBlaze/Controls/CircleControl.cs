using ModelingEvolution.Drawing;
using SkiaSharp;

namespace BlazorBlaze;

public class CircleControl : ShapeControl
{
    private readonly ObservableProperty<Control, SKPoint> _center;
    private readonly ObservableProperty<Control, float?> _hitRadius;
    private readonly ObservableProperty<Control, float> _radius;
    

    public CircleControl(SKPoint center, float radius)
    {
        _radius = new ObservableProperty<Control, float>(radius);
        _hitRadius = new ObservableProperty<Control, float?>(radius);
        _center = new ObservableProperty<Control, SKPoint>(center);
       
    }

    public float? HitRadius
    {
        get => _hitRadius.Value;
        set => _hitRadius.Change(this, value);
    }
    

    public float Radius
    {
        get => _radius.Value;
        set => _radius.Change(this, value);
    }

    public Point<float> Center
    {
        get => new(_center.Value.X, _center.Value.Y);
        set
        {
            var c = new SKPoint(value.X, value.Y);
            if (_center.Value != c) _center.Change(this, c);
        }
    }
   
    public IObservable<ObservableProperty<Control, float>.Args> ObservableRadius()
    {
        return _radius.AsObservable();
    }

    public IObservable<ObservableProperty<Control, SKPoint>.Args> ObservableCenter()
    {
        return _center.AsObservable();
    }

    public IObservable<ObservableProperty<Control, float?>.Args> ObservableHitRadius()
    {
        return _hitRadius.AsObservable();
    }

    public override void Render(SKCanvas canvas, SKRect viewport)
    {
        if (PaintStyle != SKPaintStyle.Stroke)
        {
            using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = Fill };
            canvas.DrawCircle(_center, _radius, fill);
        }
        if (StrokeWidth == 0) return;
        if (PaintStyle != SKPaintStyle.Fill)
        {
            using var stroke = new SKPaint
                { Style = SKPaintStyle.Stroke, Color = Stroke, IsStroke = true, StrokeWidth = StrokeWidth };
            canvas.DrawCircle(_center, _radius, stroke);
        }
    }

    public override void RenderForHitMap(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(_center, HitRadius ?? _radius, paint);
    }

    protected override void Dispose(bool disposing)
    {
        _center.Dispose();
        _radius.Dispose();
        base.Dispose(disposing);
    }
}