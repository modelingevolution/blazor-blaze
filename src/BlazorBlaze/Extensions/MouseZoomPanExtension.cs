using System.Numerics;

namespace BlazorBlaze;

class MouseZoomPanExtension : IEngineExtension
{
    private  BlazeEngine _engine;
    private bool _isPanning;
    private Vector2 _lastMousePosition;
    private void OnMouseWheel(object? sender, WheelMouseEventArgs e)
    {
        float factor = 1f - e.DeltaY / 2000f;
        _engine.Scene.Camera.ZoomAtPoint(factor, e.WorldAbsoluteLocation);
    }
   
    public void Bind(BlazeEngine engine)
    {
        _engine = engine;
        engine.Scene.Root.OnMouseWheel += OnMouseWheel;
       
    }

    public void Unbind(BlazeEngine engine)
    {
        engine.Scene.Root.OnMouseWheel -= OnMouseWheel;
     
    }
}