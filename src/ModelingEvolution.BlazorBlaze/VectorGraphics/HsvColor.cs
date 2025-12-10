namespace ModelingEvolution.BlazorBlaze.VectorGraphics;

public readonly record struct HsvColor(double H, double S, double V, byte A = 255)
{
    public override string ToString()
    {
        return A == 255 ? $"H: {H:F2}, S: {S:F2}%, V: {V:F2}%" : $"H: {H:F2}, S: {S:F2}%, V: {V:F2}%, A: {A}";
    }

    public static implicit operator RgbColor(HsvColor hsv)
    {
        double h = hsv.H;
        double s = hsv.S / 100.0;
        double v = hsv.V / 100.0;
        int i = (int)(h / 60) % 6;
        double f = h / 60 - i;
        double p = v * (1 - s);
        double q = v * (1 - f * s);
        double t = v * (1 - (1 - f) * s);
        double r = 0, g = 0, b = 0;
        switch (i)
        {
            case 0:
                r = v;
                g = t;
                b = p;
                break;
            case 1:
                r = q;
                g = v;
                b = p;
                break;
            case 2:
                r = p;
                g = v;
                b = t;
                break;
            case 3:
                r = p;
                g = q;
                b = v;
                break;
            case 4:
                r = t;
                g = p;
                b = v;
                break;
            case 5:
                r = v;
                g = p;
                b = q;
                break;
        }

        return new RgbColor((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), hsv.A);

    }
}