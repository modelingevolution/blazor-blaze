using System.Drawing;

namespace ModelingEvolution.BlazorBlaze;

public class BlazeEngine
{
    
    private readonly ExtensionsCollection<IEngineExtension> _extensions = new();
    
    internal ExtensionsCollection<IEngineExtension> Extensions => _extensions;



    public BlazeEngine(Size size)
    {
        Scene = new Scene(size.Width, size.Height);
        Scene.Root.EventManager = EventManager = new EventManager(Scene.HandleMouseEvent, Scene.HandleMouseEvent, Scene.HandleKeyEvent);
        Scene.Root.Engine = this;
        
        Scene.ControlAdded += (s, c) =>
        {
            c.EventManager = EventManager;
            c.Engine = this;
            c.OnInitialized(); 
        };
        Cursor = new MouseCursor<Scene>(Scene);
        
        _extensions.OnExtensionAdded += (s,e) => { e.Bind(this);};
        _extensions.OnExtensionRemoved += (s, e) => { e.Unbind(this); };
        // Order is important!
        try
        {
            _extensions.Enable<MouseOverExtension>();
            _extensions.Enable<MouseCursorExtension>();
            _extensions.Enable<MouseZoomPanExtension>();
            _extensions.Enable<CameraMoveExtension>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Extension initialization failed.");
            throw;
        }
    }

    

    public Scene Scene { get; }
    public EventManager EventManager { get; }
    public MouseCursor<Scene> Cursor { get; }
}