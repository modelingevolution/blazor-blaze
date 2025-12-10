using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Components.Web;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze;

public abstract class Control : IDisposable, IComparable<Control>
{
    public static readonly BubbleEvent<Control, MouseEventArgs> MouseDown = new(RaiseMouseDown);
    public static readonly BubbleEvent<Control, MouseEventArgs> MouseMove = new(RaiseMouseMove);

    public static readonly BubbleEvent<Control, MouseEventArgs> MouseUp = new(RaiseMouseUp);
    public static readonly BubbleEvent<Control, MouseEventArgs> Click = new(RaiseClick);
    public static readonly BubbleEvent<Control, MouseEventArgs> DbClick = new(RaiseDbClick);
    public static readonly BubbleEvent<Control, KeyboardEventArgs> KeyPress = new(RaiseKeyPress);
    public static readonly BubbleEvent<Control, KeyboardEventArgs> KeyDown = new(RaiseKeyDown);
    public static readonly BubbleEvent<Control, KeyboardEventArgs> KeyUp = new(RaiseKeyUp);
    public static readonly BubbleEvent<Control, MouseEventArgs> MouseEnter = new(RaiseMouseEnter);
    public static readonly BubbleEvent<Control, MouseEventArgs> MouseLeave = new(RaiseMouseLeave);
    public static readonly BubbleEvent<Control, WheelMouseEventArgs> MouseWheel = new(RaiseMouseWheel);
    
    public ExtensionsCollection<IControlExtension> Extensions
    {
        get
        {

            if (_extensions != null) return _extensions;

            _extensions = new();
            _extensions.OnExtensionAdded += (s, e) =>
            {
                if (Engine != null!)
                    e.Bind(this, Engine);
            };
            _extensions.OnExtensionRemoved += (s, e) =>
            {
                if (Engine != null!)
                    e.Unbind(this, Engine);
            };

            return _extensions;
        }
    }

    public object Tag { get; set; }
    public T TagCast<T>() => (T)this.Tag;
    
    public event EventHandler<MouseEventArgs> OnMouseUp;
    public event EventHandler<MouseEventArgs> OnMouseEnter;
    public event EventHandler<MouseEventArgs> OnMouseLeave;
    public event EventHandler<WheelMouseEventArgs> OnMouseWheel;
    public event EventHandler<MouseEventArgs> OnMouseMove;
    public event EventHandler<MouseEventArgs> OnMouseDown;
    public event EventHandler<MouseEventArgs> OnClick;
    public event EventHandler<MouseEventArgs> OnDbClick;
    public event EventHandler<KeyboardEventArgs> OnKeyPress;
    public event EventHandler<KeyboardEventArgs> OnKeyDown;
    public event EventHandler<KeyboardEventArgs> OnKeyUp;
    public event EventHandler<SKPoint> OnOffsetChanged;
    private static void RaiseMouseUp(Control target, Control owner, MouseEventArgs arg2) => target?.OnMouseUp?.Invoke(owner, arg2);
    private static void RaiseMouseEnter(Control target, Control owner, MouseEventArgs arg2) => target?.OnMouseEnter?.Invoke(owner, arg2);
    private static void RaiseMouseLeave(Control target, Control owner, MouseEventArgs arg2) => target?.OnMouseLeave?.Invoke(owner, arg2);
    private static void RaiseMouseWheel(Control target, Control owner, WheelMouseEventArgs arg2) => target?.OnMouseWheel?.Invoke(owner, arg2);
    private static void RaiseMouseDown(Control target, Control owner, MouseEventArgs arg2) => target?.OnMouseDown?.Invoke(owner, arg2);
    private static void RaiseMouseMove(Control target, Control owner, MouseEventArgs arg2) => target?.OnMouseMove?.Invoke(owner, arg2);
    private static void RaiseClick(Control target, Control owner, MouseEventArgs arg2) => target?.OnClick?.Invoke(owner, arg2);
    private static void RaiseDbClick(Control target, Control owner, MouseEventArgs arg2) => target?.OnDbClick?.Invoke(owner, arg2);
    private static void RaiseKeyPress(Control target, Control owner, KeyboardEventArgs args) => target.OnKeyPress?.Invoke(owner, args);
    private static void RaiseKeyUp(Control target, Control owner, KeyboardEventArgs args) => target.OnKeyUp?.Invoke(owner, args);
    private static void RaiseKeyDown(Control target, Control owner, KeyboardEventArgs args) => target.OnKeyDown?.Invoke(owner, args);
    protected Control()
    {
        // TODO: The event should propagate. 
        Cursor = new MouseCursor<Control>(this);
        _isVisible = new ObservableProperty<Control, bool>(true);
    }

    public IObservable<ObservableProperty<Control, bool>.Args> ObservableIsVisible()
    {
        return _isVisible.AsObservable();
    }

    //private int _zIndex;
    private Control? _parent;
    private ExtensionsCollection<IControlExtension>? _extensions;
    private BlazeEngine? _engine;
    private readonly ObservableProperty<Control, bool> _isVisible;

    public bool IsVisible
    {
        get => _isVisible.Value;
        set => _isVisible.Change(this, value);
    }

    private readonly ObservableProperty<Control, bool> _isHitEnabled = new ObservableProperty<Control, bool>(true);

    
    public bool IsHitEnabled
    {
        get => _isHitEnabled.Value;
        set => _isHitEnabled.Change(this, value);
    }
    public IObservable<ObservableProperty<Control, bool>.Args> ObservableIsHitEnabled() => _isHitEnabled.AsObservable();


    private ObservableProperty<Control, int> _zIndex = new(0);

    private readonly ObservableProperty<Control, SKPoint> _offset = new(new SKPoint(0, 0));
    public SKPoint Offset
    {
        get => _offset.Value;
        set => _offset.Change(this, value);
    }
    public IObservable<ObservableProperty<Control, SKPoint>.Args> ObservableOffset() => _offset.AsObservable();

    public int ZIndex
    {
        get => _zIndex.Value; set => _zIndex.Change(this, value);
    }

    public IObservable<ObservableProperty<Control, int>.Args> ObservableZIndex() => _zIndex.AsObservable();

    public MouseCursor<Control> Cursor { get; }

    public uint Id { get; internal set; }

    public SKPoint AbsoluteOffset
    {
        get { return TraverseRoot().Aggregate(new SKPoint(), (acc, x) => acc + x.Offset); }
    }

    public Control? Parent
    {
        get => _parent;
        internal set => _parent = value;
    }

    public EventManager EventManager { get; internal set; }
    protected internal virtual void OnInitialized() { }
    internal DisposableCollection? EngineRegistrations { get; set; }
    public BlazeEngine? Engine
    {
        get => _engine;
        internal set
        {
            if (_engine == value) return;
            
            if (value != null)
            {
                _engine = value;
                EngineRegistrations = new DisposableCollection();
                if (_extensions == null) return;

                foreach (var extension in _extensions)
                    extension.Bind(this, value);
            }
            else
            {
                OnUnbindEngine(_engine);
                UnbindExtensions();
                
                EngineRegistrations?.Dispose();
                EngineRegistrations = null;

                _engine = value;
            }
        }
    }

    protected virtual void OnUnbindEngine(BlazeEngine e)
    {
        
    }
    internal void UnbindExtensions()
    {
        if (_extensions == null) return;
        foreach (var extension in _extensions)
            extension.Unbind(this, _engine);
        _extensions = null;
    }

    public IEnumerable<Control> TraverseRoot()
    {
        yield return this;
        var tmp = Parent;
        while (tmp != null)
        {
            yield return tmp;
            tmp = tmp.Parent;
        }
    }



    public abstract void Render(SKCanvas canvas, SKRect viewport);
    public abstract void RenderForHitMap(SKCanvas canvas, SKPaint paint);

    private Dictionary<IBubbleEvent, Delegate>? _invocation;

    public void Subscribe<TOwner, TPayload>(IBubbleEvent evt, Action<TOwner, TPayload> action)
    {
        if (evt.OwnerType != typeof(TOwner) || evt.PayloadType != typeof(TPayload))
            throw new ArgumentException("Action delegate doesn't have matching parameter types.");

        _invocation ??= new();
        if (_invocation.TryGetValue(evt, out var ownerInvocation))
        {
            var ownerAction = (Action<TOwner, TPayload>)ownerInvocation;
            ownerAction += action;
            _invocation[evt] = ownerAction;
        }
        else
        {
            _invocation.Add(evt, action);
        }
    }
    internal void OnRaise(IBubbleEvent evt, object owner, object payload)
    {
        if (_invocation == null) return;
        if (!_invocation.TryGetValue(evt, out var action)) return;

        evt.InvokeDelegate(action, owner, payload);
    }

    private bool _disposed = false;
    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Dispose(true);
        
        _offset.Dispose();
        _zIndex.Dispose();
        _isVisible.Dispose();
        Cursor.Dispose();
    }

    public int CompareTo(Control? other)
    {
        return this.Id.CompareTo(other.Id);
    }

    public bool HasExtension<T>() => _extensions != null && _extensions.Contains<T>();
}