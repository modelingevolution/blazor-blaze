namespace BlazorBlaze;

public static class BrowserStrokeWidthPropertyExtensions
{
    public static BrowserStrokeWidthProperty BrowserStrokeWidth(this ShapeBaseControl control) => control.Extensions.GetOrAdd<BrowserStrokeWidthProperty>();
}