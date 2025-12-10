using SkiaSharp;

namespace ModelingEvolution.Blaze;

public class RectangleControl : ShapeControl
{
    private readonly ObservableProperty<Control, SKRect> _rect;

    public RectangleControl(SKRect rect)
    {
        _rect = new ObservableProperty<Control, SKRect>(rect);
    }

    public SKRect Rect
    {
        get => _rect.Value;
        set => _rect.Change(this, value);
    }

    public IObservable<ObservableProperty<Control, SKRect>.Args> ObservableRect()
    {
        return _rect.AsObservable();
    }

    public override void Render(SKCanvas canvas, SKRect viewport)
    {
        var r = _rect.Value;
        
        if (this.PaintStyle != SKPaintStyle.Stroke)
        {
            using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = Fill };
            canvas.DrawRect(r, fill);
        }
        if (StrokeWidth == 0) return;
        if (this.PaintStyle != SKPaintStyle.Fill)
        {
            using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, Color = Stroke, StrokeWidth = StrokeWidth };
            canvas.DrawRect(r, stroke);
        }
    }

    public override void RenderForHitMap(SKCanvas canvas, SKPaint paint)
    {
        
        var r = _rect.Value;
        
        canvas.DrawRect(r, paint);
    }

    protected override void Dispose(bool disposing)
    {
        _rect.Dispose();
        base.Dispose(disposing);
    }
}