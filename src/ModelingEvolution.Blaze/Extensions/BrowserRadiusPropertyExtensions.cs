namespace ModelingEvolution.Blaze;

public static class BrowserRadiusPropertyExtensions
{
    public static BrowserRadius BrowserRadius(this ShapeBaseControl control) => control.Extensions.GetOrAdd<BrowserRadius>();
}