using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace ModelingEvolution.Blaze;

public readonly record struct ScalingArgs(float Value, float DevicePixelRatio)
{
    public float Computed => Value * DevicePixelRatio;
}
public class Camera : INotifyPropertyChanged
{
    
    private bool _isInvTransformationValid;
    private float _scale = 1; // initial zoom level
    //private float _scale = 2880/1920f; // initial zoom level
    private SKMatrix _invTransformation;
    private SKPoint _position = new(0, 0); // initial position
    private SKPoint _pivot;
    private SKMatrix _transformation = SKMatrix.Identity;
    private SKRect _viewPort;
    private SKSize _browserControlSize = new SKSize(1920, 1080);
    private float _devicePixelRatio;
    public event EventHandler<ScalingArgs> ScaleChanged; 
    public Camera(Size browserControlSize)
    {
        this.BrowserControlSize = new SKSize(browserControlSize.Width, browserControlSize.Height);
    }
    public SKPoint Offset => _position;
    public float Scale => _scale;

    /// <summary>
    ///     Canvas to browser
    /// </summary>
    /// <value>
    ///     The transformation matrix.
    /// </value>
    public SKMatrix Transformation
    {
        get => _transformation;
        private set
        {
            _transformation = value;
            _isInvTransformationValid = false;
        }
    }

    public RangeF ScaleRange { get; set; } = new RangeF(float.Epsilon, float.MaxValue);

    public AreaRange AreaRange { get; set; } = AreaRange.Max;

    /// <summary>
    ///     Browser coordinates to canvas.
    /// </summary>
    /// <value>
    ///     The inverse transformation matrix.
    /// </value>
    public SKMatrix InvTransformation
    {
        get
        {
            CheckInverse();
            return _invTransformation;
        }
    }

    public SKRect ViewPort
    {
        get => _viewPort;
        set
        {
            if (_viewPort == value) return;
            _viewPort = value;
        }
    }

    public SKSize BrowserControlSize
    {
        get => _browserControlSize;
        set => SetField(ref _browserControlSize, value);
    }

    public float DevicePixelRatio
    {
        get => _devicePixelRatio;
        set => SetField(ref _devicePixelRatio, value);
    }

    public void Fit(Size logicalSize)
    {
        float scaleX = BrowserControlSize.Width / logicalSize.Width;
        float scaleY = BrowserControlSize.Height / logicalSize.Height;

        // Take smaller scale to fit both dimensions
        float fitScale = Math.Min(scaleX, scaleY);

        // Adjust for device pixel ratio
        _scale = fitScale / DevicePixelRatio;

        Transformation = GetTransformMatrix();
        ScaleChanged?.Invoke(this, new ScalingArgs(_scale, DevicePixelRatio));
        OnPropertyChanged(nameof(Scale));
    }
   

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckInverse()
    {
        if (_isInvTransformationValid) return;
        _invTransformation = _transformation.Invert();
    }

    public SKPoint MapBrowserToWorld(SKPoint point)
    {
        CheckInverse();
        return _invTransformation.MapPoint(point);
    }
    public SKSize MapBrowserToWorld(SKSize point)
    {
        CheckInverse();
        var tmp = _invTransformation.MapPoint(new SKPoint(point.Width, point.Height));
        return new SKSize(tmp.X, tmp.Y);
    }
    public SKRect MapBrowserToWorld(SKRect rect)
    {
        CheckInverse();
        return _invTransformation.MapRect(rect);
    }

    public SKPoint MapWorldToBrowser(SKPoint point) => _transformation.MapPoint(point);
    public SKRect MapWorldToBrowser(SKRect rect) => _transformation.MapRect(rect);

    public void Move(SKPoint deltaInWorld)
    {
        _position += deltaInWorld;
        
        Transformation = GetTransformMatrix();
        //AdjustViewArea();
        OnPropertyChanged(nameof(Offset));
    }

    private void AdjustViewArea()
    {
        // we know ViewAreaRange. We also know BrowserSize.
        // Let's see how the ViewAreaRange would be mapped to browser coordinates. We need to calculate it.
        // Then we can understand if it needs to be adjusted:
        // If our ViewAreaRange is not outside the BrowserView, so starts somewhere inside the BrowserView - it means that we need to adjust. We don't want to render empty space.
        // ViewPort is not reliable. We need to adjust _position. This is the offset vector in real-world coordinates.

        var browserView = MapWorldToBrowser(AreaRange.Rect);

        if (browserView.Left <= 0 && browserView.Top <= 0 && browserView.Right >= this.BrowserControlSize.Width && browserView.Bottom >= BrowserControlSize.Height)
            return;

        float dx = 0, dy = 0;
        if(browserView.Left > 0)
            dx = browserView.Left;
        else if(browserView.Right < BrowserControlSize.Width)
            dx = browserView.Right - BrowserControlSize.Width;
            
        if(browserView.Top > 0)
            dy = browserView.Top;
        else if (browserView.Bottom < BrowserControlSize.Height) 
            dy = browserView.Bottom - BrowserControlSize.Height;

        SKPoint d = new SKPoint(dx, dy);
        // lets map vector back to real-world coordinates
        d = _transformation.MapVector(d);
        _position -= d;
        Transformation = GetTransformMatrix();
    }


    public void Zoom(float factor) => _scale *= factor;

    public void ZoomAtPoint(float factor, SKPoint worldPosition)
    {
        var prv = _pivot;
        _pivot = MapWorldToBrowser(worldPosition);

        _scale = ScaleRange.Clamp(_scale * factor);

        Transformation = GetTransformMatrix();
        if (_pivot != prv)
        {
            var pivot2 = MapWorldToBrowser(worldPosition);
            
            var d = _pivot - pivot2;
            d = d.Div(_scale);
            _position += d;
            Transformation = GetTransformMatrix();
        }
        
        ScaleChanged?.Invoke(this,new ScalingArgs(_scale, DevicePixelRatio));
    }

    //private SKMatrix GetTransformMatrix()
    //{
    //    var i = SKMatrix.Identity;
    //    var s = SKMatrix.CreateScale(_scale, _scale, _pivot.X, _pivot.Y);
    //    var t = SKMatrix.CreateTranslation(_position.X, _position.Y);
    //    return SKMatrix.Concat(SKMatrix.Concat(i, s), t);
    //}
    private SKMatrix GetTransformMatrix()
    {
        var i = SKMatrix.Identity;

        // Device pixel ratio scaling
        var dpr = SKMatrix.CreateScale(DevicePixelRatio, DevicePixelRatio);

        // User zoom scaling with pivot
        var zoom = SKMatrix.CreateScale(_scale, _scale, _pivot.X, _pivot.Y);

        // Position translation
        var translation = SKMatrix.CreateTranslation(_position.X, _position.Y);

        // Compose transforms: Identity -> DPR -> Zoom -> Translation
        return SKMatrix.Concat(SKMatrix.Concat(SKMatrix.Concat(i, dpr), zoom), translation);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public void ResetScale()
    {
        _scale = 1;
        Transformation = GetTransformMatrix();
        ScaleChanged?.Invoke(this, new ScalingArgs(_scale, DevicePixelRatio));
    }
}