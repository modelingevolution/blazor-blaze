using SkiaSharp;

namespace ModelingEvolution.Blaze;


public class LineControl : ShapeBaseControl
{
    private readonly ObservableProperty<Control, SKPoint> _endPoint;
    private readonly ObservableProperty<Control, SKPoint> _startPoint;

    public LineControl(SKPoint startPoint, SKPoint endPoint)
    {
        _startPoint = new ObservableProperty<Control, SKPoint>(startPoint);
        _endPoint = new ObservableProperty<Control, SKPoint>(endPoint);
    }

    public SKPoint StartPoint
    {
        get => _startPoint.Value;
        set => _startPoint.Change(this, value);
    }

    public SKPoint EndPoint
    {
        get => _endPoint.Value;
        set => _endPoint.Change(this, value);
    }

    public IObservable<ObservableProperty<Control, SKPoint>.Args> ObservableStartPoint()
    {
        return _startPoint.AsObservable();
    }

    public IObservable<ObservableProperty<Control, SKPoint>.Args> ObservableEndPoint()
    {
        return _endPoint.AsObservable();
    }

    public override void Render(SKCanvas canvas, SKRect viewport)
    {
        using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, Color = Stroke, StrokeWidth = StrokeWidth };
        canvas.DrawLine(_startPoint.Value, _endPoint.Value, stroke);
    }

    public override void RenderForHitMap(SKCanvas canvas, SKPaint paint)
    {
        paint.StrokeWidth = HitWidth ?? StrokeWidth;
        canvas.DrawLine(_startPoint.Value, _endPoint.Value, paint);
    }

    protected override void Dispose(bool disposing)
    {
        _startPoint.Dispose();
        _endPoint.Dispose();
        base.Dispose(disposing);
    }
}