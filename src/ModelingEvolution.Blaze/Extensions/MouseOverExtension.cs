namespace ModelingEvolution.Blaze;

class MouseOverExtension : IEngineExtension
{
    private  Scene _scene;
    public event EventHandler<MouseOverControlChangedArgs>? MouseHoverControlChanged;

    private Control? _lastControl;
    private Control? _current;

    Control? Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            var prv = _current;
            _current = value;
            MouseHoverControlChanged?.Invoke(_scene, new MouseOverControlChangedArgs() { Previous = prv, Current = _current});
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (sender is not Control c) return;
        if (object.ReferenceEquals(_lastControl, c)) return;

        if (_lastControl != null) 
            _lastControl.PropagateEvent(Control.MouseLeave, e);
        
        c.PropagateEvent(Control.MouseEnter, e);
        Current = c;
        _lastControl = c;
    }

    public void Bind(BlazeEngine engine)
    {
        _scene = engine.Scene;
        _scene.Root.OnMouseMove += OnMouseMove;
    }

    public void Unbind(BlazeEngine engine)
    {
        _scene.Root.OnMouseMove -= OnMouseMove;
    }
}