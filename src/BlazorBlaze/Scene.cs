using System.Diagnostics;
using System.Drawing;
using Microsoft.AspNetCore.Components.Web;
using SkiaSharp;
using WebKeyboardEventArgs = Microsoft.AspNetCore.Components.Web.KeyboardEventArgs;

namespace BlazorBlaze;
using WebMouseEventArgs = Microsoft.AspNetCore.Components.Web.MouseEventArgs;
using WebWheelEventArgs = Microsoft.AspNetCore.Components.Web.WheelEventArgs;

public class Scene
{
    private readonly HitMap _hitMap;
    private readonly ControlIdPool _controlIdPool = new();
    private readonly SortedList<int, SortedSet<Control>> _renderTree = new();
    private readonly RootControl _root;
    private Size _size;
    public RootControl Root => _root;
    public HitMap HitMap => _hitMap;
    public Scene(int width, int height)
    {
        _size = new Size(width, height);
        Camera = new Camera(Size);
        Camera.AreaRange = Size;
        _hitMap = new HitMap(width, height);
        _root = new RootControl(() => Size) {Id=0};
        GetLayer(_root.ZIndex).Add(_root);
    }

    public void Fit()
    {
        this.Camera.Fit(Size);
    }
    public Size Size
    {
        get => _size;
        internal set
        {
            if (_size == value)
                return;
            //Console.WriteLine("Resize!");
            _size = value;
            Camera.AreaRange = Size;
            Camera.BrowserControlSize = new SKSize(Size.Width, Size.Height);
            _hitMap.Resize(_size);
        }
    }

    public Camera Camera { get; }
    public event EventHandler<Control> ControlAdded;
    public event EventHandler<Control> ControlRemoved;

    public void AddControl(Control control)
    {
        // Let's travel to the top;
        var root = control.TraverseRoot().Last();
        bool isRooted = root == this.Root;

        if (!isRooted)
            root.Parent = _root;
        else 
            root = control;
        
        Debug.Assert(control.Parent != null);
        
        foreach(var x in root.Tree())
        {
            Debug.Assert(x.Parent != null);
            if(x.ZIndex == 0)
                x.ZIndex = control.ZIndex;
            
            var r1 = x.ObservableZIndex().Subscribe(x => OnControlZIndexChanged(x.Sender, x.Previous, x.Current));
            var r2 = x.ObservableIsVisible().Subscribe(x => OnControlIsVisibleChanged(x.Sender, x.Previous, x.Current));
            x.Id = _controlIdPool.Rent();
            if(x.IsVisible)
                GetLayer(x.ZIndex).Add(x);
            ControlAdded.Invoke(this, x);
            x.EngineRegistrations!.Add(r1).Add(r2);
        }

    }

    private void OnControlIsVisibleChanged(Control x, bool objPrevious, bool objCurrent)
    {
        if (objCurrent)
            GetLayer(x.ZIndex).Add(x);
        else GetLayer(x.ZIndex).Remove(x);
    }

    private SortedSet<Control> GetLayer(int zIndex)
    {
        if (_renderTree.TryGetValue(zIndex, out var layer)) return layer;

        layer = new SortedSet<Control>();
        _renderTree.Add(zIndex, layer);
        return layer;
    }

    private void OnControlZIndexChanged(Control control, int prv, int current)
    {
        var prvLayer = _renderTree[prv];
        var r = prvLayer.Remove(control);
        Debug.Assert(r);
        GetLayer(current).Add(control);
    }

    public void Render(SKCanvas canvas, SKRect viewport)
    {
        _hitMap.Clear();
        
        canvas.SetMatrix(this.Camera.Transformation);
        int renderedObjects = 0;
        foreach (var zIndexLayer in _renderTree)
        foreach (var control in zIndexLayer.Value)
        {
            if(!control.IsVisible) continue;
            
            Debug.Assert(control.ZIndex == zIndexLayer.Key);
            canvas.Save();
            _hitMap.Save();

            var off = control.AbsoluteOffset;
            canvas.Translate(off);
            _hitMap.Translate(off);

            control.Render(canvas, viewport);
            if(control.IsHitEnabled)
                _hitMap.Render(control);

            _hitMap.Restore();
            canvas.Restore();
            
            renderedObjects += 1;
        }
        //Console.WriteLine($"Rendered objects: {renderedObjects}");
        _hitMap.Flush();
    }
    
    public void HandleMouseEvent(WebMouseEventArgs args, IBubbleEvent<MouseEventArgs> evt) => HandleMouseEvent(evt, args);
    public void HandleMouseEvent(WebWheelEventArgs args, IBubbleEvent<WheelMouseEventArgs> evt) => HandleMouseEvent(evt, args);
    public void HandleKeyEvent(WebKeyboardEventArgs arg1, IBubbleEvent<KeyboardEventArgs> arg2)
    {
        var control= this.Root.FocusedControl;
        control?.PropagateEvent(arg2, arg1);
    }
    private void HandleMouseEvent<TWeb,TBlazeEvent>(IBubbleEvent<TBlazeEvent> evt, TWeb tmp) 
        where TBlazeEvent:IMouseEventArgs<TBlazeEvent,TWeb>
        where TWeb:Microsoft.AspNetCore.Components.Web.MouseEventArgs
    {
        var worldLocation = this.Camera.MapBrowserToWorld(new SKPoint((float)tmp.OffsetX, (float)tmp.OffsetY));
        var control = _hitMap.GetControlAt(worldLocation);
        
        if (control == null) return;
        //Console.WriteLine($"Control {control.Id} at {worldLocation.ToShortString()}");
        
        var e = TBlazeEvent.ConvertFrom(tmp, Camera);
        
        control.PropagateEvent(evt, e);
    }


    public void RemoveControl(Control? control)
    {
        if(control == null) return;
        foreach (var c in control.Tree().Reverse())
        {
            if (this.GetLayer(c.ZIndex).Remove(c))
            {
                ControlRemoved?.Invoke(this, c);
                c.Engine = null;
            }
            //else
            //    Debug.Fail("Cannot find control");
        }

        var p = control.Parent; // we assume that this is indeed our control-tree.
        if (p == null) return; // control was never added.

        _controlIdPool.Return(control.Id);
            
        control.Parent = null;
        if (p is ContentControl cc) cc.Content = null;
        else if (p is ItemsControl ic) ic.Children.Remove(control);
    }

   
}