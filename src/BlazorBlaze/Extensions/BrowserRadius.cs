namespace BlazorBlaze;

public class BrowserRadius : IControlExtension
{
    private ObservableProperty<CircleControl, float>? _radius;
    private CircleControl _owner;
    private IDisposable? _op;

    public float Radius
    {
        get => _radius.Value;
        set
        {
            if (_radius == null)
            {
                _radius = new ObservableProperty<CircleControl, float>(value);
                _op = _radius.AsObservable().Subscribe(OnWidthChanged);
            }
            _radius!.Change(_owner, value);
        }
    }

    private void OnWidthChanged(ObservableProperty<CircleControl, float>.Args args)
    {
        _owner.StrokeWidth = args.Current;
    }

    public void Bind(Control control, BlazeEngine engine)
    {
        _owner = (CircleControl)control;
        var ee = engine.Extensions.GetOrAdd<BrowserRadiusExtensionExtension>();
        ee.Register(_owner);
    }

    public void Unbind(Control control, BlazeEngine engine)
    {
        var ee = engine.Extensions.GetOrAdd<BrowserRadiusExtensionExtension>();
        ee.Unregister(_owner);
        _op?.Dispose();
    }
}