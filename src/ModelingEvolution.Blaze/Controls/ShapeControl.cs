using SkiaSharp;

namespace ModelingEvolution.Blaze;

public abstract class ShapeBaseControl : Control
{
    private readonly ObservableProperty<Control, SKColor> _stroke = new(SKColors.Black);
    private readonly ObservableProperty<Control, float> _strokeWidth = new(1);
    private readonly ObservableProperty<Control, float?> _hitWidth = new(1);
    public SKColor Stroke
    {
        get => _stroke.Value;
        set => _stroke.Change(this, value);
    }
    public float? HitWidth
    {
        get => _hitWidth.Value;
        set => _hitWidth.Change(this, value);
    }
    public float StrokeWidth
    {
        get => _strokeWidth.Value;
        set => _strokeWidth.Change(this, value);
    }
    public IObservable<ObservableProperty<Control, SKColor>.Args> ObservableStroke()
    {
        return _stroke.AsObservable();
    }

    public IObservable<ObservableProperty<Control, float>.Args> ObservableStrokeWidth()
    {
        return _strokeWidth.AsObservable();
    }
    public IObservable<ObservableProperty<Control, float?>.Args> ObservableHitWidth()
    {
        return _hitWidth.AsObservable();
    }
    protected override void Dispose(bool disposing)
    {
        _stroke.Dispose();
        _strokeWidth.Dispose();
        _hitWidth.Dispose();
        base.Dispose(disposing);
    }
}
public abstract class ShapeControl : ShapeBaseControl
{
    private readonly ObservableProperty<Control, SKColor> _fill = new(SKColors.Transparent);
    
    private readonly ObservableProperty<Control, SKPaintStyle> _paintStyle = new(SKPaintStyle.Fill);
   
    public SKColor Fill
    {
        get => _fill.Value;
        set => _fill.Change(this, value);
    }
    
    public SKPaintStyle PaintStyle
    {
        get => _paintStyle.Value;
        set => _paintStyle.Change(this, value);
    }

    public IObservable<ObservableProperty<Control, SKColor>.Args> ObservableFill()
    {
        return _fill.AsObservable();
    }


    public IObservable<ObservableProperty<Control, SKPaintStyle>.Args> ObservablePaintStyle()
    {
        return _paintStyle.AsObservable();
    }

    protected override void Dispose(bool disposing)
    {
        _paintStyle.Dispose();
        _fill.Dispose();
        
        base.Dispose(disposing);
    }
}