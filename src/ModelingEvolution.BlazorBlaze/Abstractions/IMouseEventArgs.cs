using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze;

public interface IMouseEventArgs
{
    SKPoint ComputeRelativeTo(Control c) => c.AbsoluteOffset - WorldAbsoluteLocation;
    bool AltKey { get; }
    bool CtrlKey { get; }
    bool ShiftKey { get; }
    bool MetaKey { get; }
    long Button { get; }
    long Buttons { get; }
    SKPoint BrowserLocation { get; }

    SKPoint BrowserMovement { get; }
    SKPoint WorldAbsoluteLocation { get; }
    SKPoint WorldMovement { get; }
}
public interface IMouseEventArgs<out TSelf, in TWeb> : IMouseEventArgs
    where TWeb: Microsoft.AspNetCore.Components.Web.MouseEventArgs
{
    static abstract TSelf ConvertFrom(TWeb args, Camera cam);
    
}