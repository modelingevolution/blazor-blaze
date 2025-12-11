using System.Drawing;
using SkiaSharp;

namespace BlazorBlaze;

public static class FocusExtension
{
    public static FocusProperty EnableFocus(this Control c) => c.Extensions.GetOrAdd<FocusProperty>();
    public static void DisableFocus(this Control c) => c.Extensions.Disable<FocusProperty>();
}
public class FocusProperty : IControlExtension
{
    private readonly ObservableProperty<Control, bool> _isFocused;
    public void Bind(Control control, BlazeEngine engine)
    {
        this.Control = control;
    }

    public FocusProperty()
    {
        _isFocused = new ObservableProperty<Control, bool>(false);
    }
    public bool IsFocused
    {
        get => _isFocused.Value;
        set => _isFocused.Change(Control, value);
    }
    public Control Control { get; private set; }

    public void Unbind(Control control, BlazeEngine engine)
    {
        _isFocused.Dispose();
    }
}
public sealed class RootControl : Control
{
    private readonly ObservableProperty<Control, SKColor> _background = new(SKColors.Black);
    private readonly Func<Size> _canvasSize;

    private readonly ObservableProperty<RootControl, Control> _focusedControl;
    
    public IObservable<ObservableProperty<RootControl, Control>.Args> ObservableFocusedControl()
    {
        return _focusedControl.AsObservable();
    }

    
   

    private void OnRootControlMouseDown(object? sender, MouseEventArgs e)
    {
        var src = (Control)sender;
        foreach (var i in src.TraverseRoot())
        {
            if (!i.HasExtension<FocusProperty>()) continue;
            
            FocusedControl = i;
            break;
        }
    }

    public Control? FocusedControl
    {
        get => _focusedControl.Value;
        internal set
        {
            var prv = FocusedControl;
            if (prv == value)
                return;
            
            if(prv != null)
                prv.Extensions.GetOrAdd<FocusProperty>().IsFocused = false;
            
            _focusedControl.Change(this, value);
            
            if(value != null)
                value.Extensions.GetOrAdd<FocusProperty>().IsFocused = true;
        }
    }

    public RootControl(Func<Size> canvasSize) : base()
    {
        _canvasSize = canvasSize;
        ZIndex = -1;
        _focusedControl = new ObservableProperty<RootControl, Control>(null);
        this.OnMouseDown += OnRootControlMouseDown;
        this.EnableFocus();
    }


    public SKColor Background
    {
        get => _background.Value;
        set => _background.Change(this, value);
    }

    public IObservable<ObservableProperty<Control, SKColor>.Args> ObservableBackground() => _background.AsObservable();

    public override void Render(SKCanvas canvas, SKRect viewport)
    {
        //var s = _canvasSize();
        //using SKPaint paint = new SKPaint() { Color = Background };
        //// The display shall fit to the canvas. We don't expect the ratio not to be different more than 2x. 
        //canvas.DrawRect(0, 0, s.Width*2, s.Height*2, paint);

        //If we happen to change mind and display the browser control as background - here is the attempt.
        if (Engine == null) return;
        var browserSize = Engine.Scene.Camera.BrowserControlSize;

        // Convert browser rect to world coordinates
        var topLeft = Engine.Scene.Camera.MapBrowserToWorld(new SKPoint(0, 0));
        var bottomRight = Engine.Scene.Camera.MapBrowserToWorld(new SKSize(browserSize.Width, browserSize.Height));

        using SKPaint paint = new SKPaint() { Color = Background };
        canvas.DrawRect(SKRect.Create(topLeft, bottomRight), paint);
    }

    public override void RenderForHitMap(SKCanvas canvas, SKPaint paint)
    {
        //var s = CanvasSize();
        //canvas.DrawRect(0,0, s.Width, s.Height, paint);
    }
}