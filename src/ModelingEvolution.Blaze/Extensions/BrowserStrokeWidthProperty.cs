namespace ModelingEvolution.Blaze;

public class BrowserStrokeWidthProperty : IControlExtension
{
    private ObservableProperty<ShapeBaseControl, float>? _strokeWidth;
    private ShapeBaseControl _owner;
    private IDisposable? _op;

    public float StrokeWidth
    {
        get => _strokeWidth.Value;
        set
        {
            if (_strokeWidth == null)
            {
                _strokeWidth = new ObservableProperty<ShapeBaseControl, float>(value);
                _op = _strokeWidth.AsObservable().Subscribe(OnWidthChanged);
            }
            _strokeWidth!.Change(_owner, value);
        }
    }

    private void OnWidthChanged(ObservableProperty<ShapeBaseControl, float>.Args args)
    {
        _owner.StrokeWidth = args.Current;
    }

    public void Bind(Control control, BlazeEngine engine)
    {
        _owner = (ShapeBaseControl)control;
        var ee = engine.Extensions.GetOrAdd<BrowserStrokeWidthExtension>();
        ee.Register(_owner);
    }

    public void Unbind(Control control, BlazeEngine engine)
    {
        var ee = engine.Extensions.GetOrAdd<BrowserStrokeWidthExtension>();
        ee.Unregister(_owner);
        _op?.Dispose();
    }
}