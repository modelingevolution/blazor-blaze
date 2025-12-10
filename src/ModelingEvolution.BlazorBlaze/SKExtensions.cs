using ModelingEvolution.Drawing;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze;

public static class SkiaExtensions
{
    public static string ToShortString(this in SKPoint point)
    {
        return $"[{point.X} {point.Y}]";
    }
    public static string ToShortString(this in SKRect r)
    {
        return $"[{r.Left} {r.Top} {r.Width} {r.Height}]";
    }
    public static string ToShortString(this in SKSize r)
    {
        return $"[{r.Width} {r.Height}]";
    }
    public static Rectangle<float> ToRect(this in SKRect rect)
    {
        return new Rectangle<float>(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public static SKColor Sk(this Color c)
    {
        return new SKColor(c.R, c.G, c.B, c.A);
    }
    public static SKPoint Mul(this SKPoint point, float p) => new SKPoint(point.X * p, point.Y * p);
    public static SKPoint Div(this SKPoint point, float p) => new SKPoint(point.X /p, point.Y /p);
    public static SKPoint Sk(this Point<float> p) => new SKPoint(p.X, p.Y);
    public static SKPoint Sk(this Point<double> p) => new SKPoint((float)p.X, (float)p.Y);
    public static Point<float> AsPoint(this SKPoint p) => new Point<float>(p.X, p.Y);
    public static Point<double> AsPointD(this SKPoint p) => new Point<double>(p.X, p.Y);

    public static void LogPoints(this IEnumerable<Point<float>> points)
    {
        Console.WriteLine(string.Join(' ', points.Select(x=>$"[{x.X} {x.Y}]")));
    }
    public static uint ToId(this SKColor color)
    {
        return (uint)((color.Red & 0xFF) | ((color.Green << 8) & 0xFF00) | ((color.Blue << 16) & 0xFF0000));
    }
    /// <summary>
    /// Converts a uint to an SKColor. 
    /// Assumes the uint contains color data in the format AABBGGRR.
    /// </summary>
    /// <param name="colorValue">A uint representing the color with alpha in the most significant byte.</param>
    /// <returns>An SKColor object.</returns>
    public static SKColor ToSKColor(this uint colorValue)
    {
        // Extract individual color components from the uint
        
        byte blue = (byte)((colorValue >> 16) & 0xFF);
        byte green = (byte)((colorValue >> 8) & 0xFF);
        byte red = (byte)(colorValue & 0xFF);

        return new SKColor(red, green, blue);
    }

    /// <summary>
    /// Converts an SKColor to a uint.
    /// The uint representation will be in the format AABBGGRR.
    /// </summary>
    /// <param name="color">An SKColor to convert.</param>
    /// <returns>A uint representing the color with alpha in the most significant byte.</returns>
    public static uint ToUInt32(this SKColor color)
    {
        // Combine the color components into a uint
        return (uint)((color.Blue << 16) | (color.Green << 8) | color.Red);
    }

   
   
}