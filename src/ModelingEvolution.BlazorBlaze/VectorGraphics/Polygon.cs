using System.Drawing;
using System.Globalization;
using System.Text;
using ModelingEvolution.Drawing;
using ProtoBuf;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.VectorGraphics;

public record PolygonGraphics : IDisposable
{

    public Polygon Polygon { get; init; }
    public Size ImageSize { get; set; }

    public void TransformBy(in System.Drawing.Rectangle interestRegion)
    {
        this.Resize(interestRegion.Size);
        this.Polygon.Offset(interestRegion.Location);
    }
    public void Resize(Size newSize)
    {
        var ratio = new SizeF(((float)newSize.Width) / ImageSize.Width, ((float)newSize.Height) / ImageSize.Height);
        Polygon.ScaleBy(ratio);
        ImageSize = newSize;
    }

    public Polygon<float> NormalizedPolygon()
    {
        var polygonF = Polygon.ToPolygonF();
        var ratio = new Size<float>(1f / ImageSize.Width, 1f / ImageSize.Height);
        return polygonF * ratio;
    }
    public Polygon<float> NormalizedPolygon(Size<float> dest)
    {
        var polygonF = Polygon.ToPolygonF();
        var ratio = new Size<float>(1f / ImageSize.Width, 1f / ImageSize.Height) * dest;
        return polygonF * ratio;
    }

    public PolygonGraphics(ManagedArray<SKPoint> points, in Size size)
    {
        Polygon = new Polygon(points);
        ImageSize = size;
    }

    private void Dispose(bool disposing)
    {
        if (disposing) Polygon.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

[ProtoContract]
public record class Polygon : IRenderItem, IDisposable
{
    public Polygon()
    {

    }
    public static implicit operator Polygon(Polygon<float> polygon)
    {
        return new Polygon(polygon.Points.Select(p => new SKPoint(p.X, p.Y)).ToArray());
    }
    public static Polygon From(ReadOnlySpan<SKPoint> points)
    {
        return new Polygon(points.ToArray());
    }

    public Polygon<float> ToPolygonF()
    {
        var floatPoints = new ModelingEvolution.Drawing.Point<float>[Points.Count];
        for (int i = 0; i < Points.Count; i++)
        {
            floatPoints[i] = new ModelingEvolution.Drawing.Point<float>(Points[i].X, Points[i].Y);
        }
        return new Polygon<float>(floatPoints);
    }
    public Polygon(SKPoint[] points)
    {
        this.Points = new ManagedArray<SKPoint>(points);

    }
    public Polygon(ManagedArray<SKPoint> points)
    {
        this.Points = points;
    }
    public void ScaleBy(SizeF size)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            Points[i] = new SKPoint(p.X * size.Width, p.Y * size.Height);
        }
    }
    public void ScaleBy(Size size)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            Points[i] = new SKPoint(p.X * size.Width, p.Y * size.Height);
        }
    }

    public SKPoint this[int index] => Points[index];

    public void Offset(System.Drawing.Point offset)
    {
        for (var i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            Points[i] = new SKPoint(p.X + offset.X, p.Y + offset.Y);
        }
    }

    public string ToAnnotationString()
    {
        CultureInfo culture = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        for (int i = 0; i < Points.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            var pt = Points[i];
            sb.Append(pt.X.ToString(culture));
            sb.Append(' ');
            sb.Append(pt.Y.ToString(culture));
        }
        return sb.ToString();
    }

    [ProtoMember(1)]
    public ManagedArray<SKPoint> Points { get; init; }


    public static Polygon GenerateRandom(int count)
    {
        var tmp = new ManagedArray<SKPoint>(count);
        for (int i = 0; i < count; i++)
            tmp.Add(new SKPoint(
                Random.Shared.Next(0, ushort.MaxValue),
                Random.Shared.Next(0, ushort.MaxValue)));
        return new Polygon() { Points = tmp };
    }

    public void Render(ICanvas canvasChannel, DrawContext? context)
    {
        var ctx = context ?? default;
        canvasChannel.DrawPolygon(Points.GetBuffer().AsSpan(0, Points.Count), ctx.Stroke, ctx.Thickness);
    }

    public ushort Id => 2;



    public override string ToString()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Points.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            var p = Points[i];
            sb.Append(p.X);
            sb.Append(' ');
            sb.Append(p.Y);
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        Points.Dispose();
    }


    public string ToSvg()
    {
        // Generate Svg type path string.
        StringBuilder sb = new StringBuilder("M");
        for (int i = 0; i < Points.Count; i++)
        {
            var point = Points[i];
            sb.Append($" {point.X} {point.Y}");
        }

        sb.Append(" Z");
        return sb.ToString();
    }
}
