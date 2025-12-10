

using System.Runtime.InteropServices.ComTypes;

namespace ModelingEvolution.Blaze;

class MouseCursorExtension : IEngineExtension
{
    private BlazeEngine _engine;
    private Control lastHoveredControl;
    private IDisposable? _op;

   
    private void OnControlAdded(object? sender, Control e) => e.Cursor.ObservableType().Subscribe(x => _engine.Cursor.Type = x);


    private void OnControlChanged(object? sender, MouseOverControlChangedArgs e)
    {
        lastHoveredControl = e.Current;
        _engine.Cursor.Type = e.Current.Cursor.Type;
    }

    public void Bind(BlazeEngine engine)
    {
        _engine = engine;
        engine.Extensions.GetOrAdd<MouseOverExtension>().MouseHoverControlChanged += OnControlChanged;
        engine.Scene.ControlAdded += OnControlAdded;
        this._op = engine.Scene.Root.Cursor.ObservableType().Subscribe(x => _engine.Cursor.Type = x);
        _engine.Cursor.Type = engine.Scene.Root.Cursor.Type;
    }

    public void Unbind(BlazeEngine engine)
    {
        engine.Extensions.GetOrAdd<MouseOverExtension>().MouseHoverControlChanged -= OnControlChanged;
        engine.Scene.ControlAdded -= OnControlAdded;
        _op.Dispose();
    }
}