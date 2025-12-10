using SkiaSharp;

namespace ModelingEvolution.Blaze;

public readonly record struct AreaRange
{
    public float MinX { get; }
    public float MaxX { get; } 
    public float MinY { get; }
    public float MaxY { get; }
    public SKPoint TopLeft => new SKPoint(MinX, MinY);
    public SKPoint BottomRight => new SKPoint(MaxX, MaxY);
    public SKRect Rect => new SKRect(MinX, MinY, MaxX, MaxY);
    
    public static implicit operator AreaRange(System.Drawing.Size size) =>
        new AreaRange(0, size.Width, 0, size.Height);
    
    public static readonly AreaRange
        Max = new AreaRange(float.MinValue, float.MaxValue, float.MinValue, float.MaxValue);
    
    public AreaRange()
    {
        
    }
    public AreaRange(float minX, float maxX, float minY, float maxY)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    
    public bool Contains(SKPoint point) =>
        point.X >= MinX && point.X <= MaxX &&
        point.Y >= MinY && point.Y <= MaxY;

    
    public SKPoint Clamp(SKPoint point)
    {
        float clampedX = Math.Clamp(point.X, MinX, MaxX);
        float clampedY = Math.Clamp(point.Y, MinY, MaxY);
        return new SKPoint(clampedX, clampedY);
    }

    public override string ToString() => $"[{MinX}, {MaxX}] x [{MinY}, {MaxY}]";
}