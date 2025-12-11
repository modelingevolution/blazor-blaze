namespace BlazorBlaze;

class BrowserStrokeWidthExtension() : BrowserEngineExtension<ShapeBaseControl, BrowserStrokeWidthProperty>(OnChange)
{
    private static void OnChange(ShapeBaseControl i, ScalingArgs e)
    {
        var browserWidth = i.BrowserStrokeWidth();
        i.StrokeWidth = browserWidth.StrokeWidth / e.Computed;
    }
}