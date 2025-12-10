namespace ModelingEvolution.Blaze;

class CameraMoveExtension : IEngineExtension
{
    private BlazeEngine _engine;
    private bool _isDragging = false;
    private bool _attemptingToDrag = false;

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (sender != _engine.Scene.Root) return;
        if (e.CtrlKey || e.Button==1)
        {
            _isDragging = true;
            _engine.Scene.Root.Cursor.Type = MouseCursorType.Grab;
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (sender != _engine.Scene.Root) return;
        if (!_isDragging) return;

        var d = e.WorldMovement;
        _engine.Scene.Camera.Move(d);

    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        _attemptingToDrag = false;
        if ((e.CtrlKey || e.Button==1) && _isDragging)
        {
            _isDragging = false;
            _engine.Cursor.Type = MouseCursorType.Default;
        }

    }

    public void Bind(BlazeEngine engine)
    {
        _engine = engine;

        engine.Scene.Root.OnMouseDown += OnMouseDown;
        engine.Scene.Root.OnMouseMove += OnMouseMove;
        engine.Scene.Root.OnMouseUp += OnMouseUp;
        //Console.WriteLine("Bind camera move!");
    }

    public void Unbind(BlazeEngine engine)
    {
        engine.Scene.Root.OnMouseDown -= OnMouseDown;
        engine.Scene.Root.OnMouseMove -= OnMouseMove;
        engine.Scene.Root.OnMouseUp -= OnMouseUp;


        //Console.WriteLine("Unbind camera move!");
    }
}