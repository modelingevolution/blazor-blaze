using SkiaSharp;

namespace BlazorBlaze;


public record MouseEventArgs : IMouseEventArgs<MouseEventArgs,Microsoft.AspNetCore.Components.Web.MouseEventArgs>
{
   
    public bool AltKey { get; init; }
    public bool CtrlKey { get; init; }
    public bool ShiftKey { get; init; }
    public bool MetaKey { get; init; }
    public long Button { get; init; }
    public long Buttons { get; init; }
    public SKPoint BrowserLocation { get; init; }
    public SKPoint WorldAbsoluteLocation { get; init; }
    public SKPoint BrowserMovement { get; init; }

   public SKPoint WorldMovement { get; init; }
   

   public static MouseEventArgs ConvertFrom(Microsoft.AspNetCore.Components.Web.MouseEventArgs args, Camera cam)
    {
        var browserLocation = new SKPoint((float)args.OffsetX, (float)args.OffsetY);
        var browserMovement = new SKPoint((float)args.MovementX, (float)args.MovementY);
        return new MouseEventArgs
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
        };
    }

}