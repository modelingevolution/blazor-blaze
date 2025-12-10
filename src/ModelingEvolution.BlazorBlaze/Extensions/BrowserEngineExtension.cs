namespace ModelingEvolution.BlazorBlaze;

/// <summary>
/// Base class for extensions that maintain consistent visual sizes of control properties (like radius, stroke width) 
/// regardless of camera zoom level. When camera scale changes, the extension automatically adjusts world coordinates 
/// of registered controls to maintain their browser-pixel sizes.
/// <para>
/// Typical use case: A control button in a shape of circle control - it needs to maintain a constant 10px radius on screen regardless of zoom.
/// When user zooms in (scale increases), the extension automatically reduces the circle's world-coordinate radius
/// to compensate, ensuring it still appears as 10px on screen.
/// </para>
/// </summary>
abstract class BrowserEngineExtension<TControl, TControlExtension>(Action<TControl, ScalingArgs> ScaleChanged) : IEngineExtension
where TControlExtension:IControlExtension
where TControl:Control
{
    private readonly SortedSet<TControl> _index = new();
    private Camera _camera;

    public void Bind(BlazeEngine engine)
    {
        this._camera = engine.Scene.Camera;
        engine.Scene.Camera.ScaleChanged += OnScaleChanged;
    }

    private void OnScaleChanged(object? sender, ScalingArgs e)
    {
        foreach (var i in _index)
        {
            ScaleChanged(i, e);
        }
    }

    public void Unbind(BlazeEngine engine)
    {
        engine.Scene.Camera.ScaleChanged -= OnScaleChanged;
    }

    public void Register(TControl control)
    {
        ScaleChanged(control, new ScalingArgs( _camera.Scale, _camera.DevicePixelRatio));
        _index.Add(control);
    }

    public void Unregister(TControl control)
    {
        _index.Remove(control);
    }
}