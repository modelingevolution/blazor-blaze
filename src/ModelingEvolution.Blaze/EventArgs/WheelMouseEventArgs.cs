using SkiaSharp;

namespace ModelingEvolution.Blaze;


public record WheelMouseEventArgs: MouseEventArgs, IMouseEventArgs<WheelMouseEventArgs,Microsoft.AspNetCore.Components.Web.WheelEventArgs>
{
    public float DeltaX { get; init; }
    public float DeltaY { get; init; }
    public float DeltaZ { get; init; }
    public long DeltaMode { get; init; }

    public static WheelMouseEventArgs ConvertFrom(Microsoft.AspNetCore.Components.Web.WheelEventArgs args, Camera cam)
    {
        var browserLocation = new SKPoint((float)args.OffsetX, (float)args.OffsetY);
        var browserMovement = new SKPoint((float)args.MovementX, (float)args.MovementY);
        
        return new WheelMouseEventArgs
        {
            BrowserLocation = browserLocation,
            BrowserMovement = browserMovement,
            WorldAbsoluteLocation = cam.MapBrowserToWorld(browserLocation),
            WorldMovement = browserMovement.Div(cam.Scale),
            AltKey = args.AltKey,
            CtrlKey = args.CtrlKey,
            ShiftKey = args.ShiftKey,
            MetaKey = args.MetaKey,
            Button = args.Button,
            Buttons = args.Buttons,
            DeltaMode = args.DeltaMode,
            DeltaX = (float)args.DeltaX,
            DeltaY = (float)args.DeltaY,
            DeltaZ = (float)args.DeltaZ,

        };
    }
}