using SkiaSharp;

namespace BlazorBlaze.VectorGraphics;

public readonly record struct RgbColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly RgbColor White = new RgbColor(255, 255, 255);
    public static readonly RgbColor Black = new RgbColor(0, 0, 0);
    public static readonly RgbColor Red = new RgbColor(255, 0, 0);
    public static readonly RgbColor Blue = new RgbColor(0, 0, 255);
    public static readonly RgbColor Green = new RgbColor(0, 255, 0);
    public static readonly RgbColor Gray = new RgbColor(128, 128, 128);
    public override string ToString()
    {
        return A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";
    }
    public static implicit operator HsvColor(RgbColor rgb)
    {
        double r = rgb.R / 255.0;
        double g = rgb.G / 255.0;
        double b = rgb.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double h = 0;
        if (delta != 0)
        {
            if (max == r)
            {
                h = (g - b) / delta;
            }
            else if (max == g)
            {
                h = 2 + (b - r) / delta;
            }
            else
            {
                h = 4 + (r - g) / delta;
            }
            h *= 60;
            if (h < 0) h += 360;
        }
        double s = max == 0 ? 0 : delta / max;
        double v = max;
        return new HsvColor(h, s * 100, v * 100, rgb.A);
    }

    public static implicit operator SKColor(RgbColor rgbColor) =>
        new SKColor(rgbColor.R, rgbColor.G, rgbColor.B, rgbColor.A);
}