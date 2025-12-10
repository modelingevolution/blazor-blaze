using SkiaSharp;

namespace ModelingEvolution.Blaze;

class ControlDragExtension : ControlExtension<Control>
{
    
    private bool _isDragging = false;
    
    private IDisposable? _op;
    public override void Bind()
    {
        Control.OnMouseEnter += OnMouseEnter;
        Control.OnMouseLeave += OnMouseLeave;
        Control.OnMouseDown += OnMouseDown;
        Engine.Scene.Root.OnMouseUp += OnMouseUp;
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
        _recordingStarted = false;
        Engine.Scene.Root.OnMouseMove -= OnMouseMove;
        _op?.Dispose();
        _op = null;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        _isDragging = true;
        
    }

    private void OnMouseLeave(object? sender, MouseEventArgs e)
    {
        if(!_isDragging)
            Engine.Scene.Root.OnMouseMove -= OnMouseMove;
    }

    private void OnMouseEnter(object? sender, MouseEventArgs e)
    {
        // this is heavy operation. We need to minimalize the amount of OnMouseMove subscriptions.
        Engine.Scene.Root.OnMouseMove += OnMouseMove;
    }

    private bool _recordingStarted = false;
    private SKPoint _offset;
    private SKPoint _start;
    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        if (!_recordingStarted)
        {
            _start = e.WorldAbsoluteLocation;
            _offset = this.Control.Offset;
            _recordingStarted = true;
            _op = Control.Cursor.Change(MouseCursorType.Grabbing);
        }
        else
        {
            var o = _offset + (e.WorldAbsoluteLocation - _start);
            Control.Offset = o;
            //Console.WriteLine($"Offset: {Control.Offset}");
        }
    }

    public override void Unbind()
    {
        Control.OnMouseEnter -= OnMouseEnter;
        Control.OnMouseLeave -= OnMouseLeave;
        Control.OnMouseDown -= OnMouseDown;
        Engine.Scene.Root.OnMouseUp -= OnMouseUp;
    }
}