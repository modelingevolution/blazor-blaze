using System.Numerics;
using ModelingEvolution.Drawing;
using SkiaSharp;

namespace BlazorBlaze;

/// <summary>
/// Extension methods for drawing ModelingEvolution.Drawing primitives on SKCanvas.
/// </summary>
public static class SKCanvasDrawingExtensions
{
    // ════════════════════════════════════════════════
    //  ToSKPath conversions
    // ════════════════════════════════════════════════

    /// <summary>
    /// Converts a Polygon to a closed SKPath.
    /// </summary>
    public static SKPath ToSKPath<T>(this Polygon<T> polygon)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        var path = new SKPath();
        if (polygon.Count == 0) return path;

        path.MoveTo(F(polygon[0].X), F(polygon[0].Y));
        for (int i = 1; i < polygon.Count; i++)
            path.LineTo(F(polygon[i].X), F(polygon[i].Y));
        path.Close();
        return path;
    }

    /// <summary>
    /// Converts a Polyline to an open SKPath (no Close).
    /// </summary>
    public static SKPath ToSKPath<T>(this in Polyline<T> polyline)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        var path = new SKPath();
        var span = polyline.AsSpan();
        if (span.Length == 0) return path;

        path.MoveTo(F(span[0].X), F(span[0].Y));
        for (int i = 1; i < span.Length; i++)
            path.LineTo(F(span[i].X), F(span[i].Y));
        return path;
    }

    /// <summary>
    /// Converts a cubic BezierCurve to an SKPath with a single CubicTo.
    /// </summary>
    public static SKPath ToSKPath<T>(this in BezierCurve<T> bezier)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        var path = new SKPath();
        path.MoveTo(F(bezier.Start.X), F(bezier.Start.Y));
        path.CubicTo(
            F(bezier.C0.X), F(bezier.C0.Y),
            F(bezier.C1.X), F(bezier.C1.Y),
            F(bezier.End.X), F(bezier.End.Y));
        return path;
    }

    /// <summary>
    /// Converts a ComplexCurve (mixed polyline + Bezier segments) to an SKPath.
    /// </summary>
    public static SKPath ToSKPath<T>(this in ComplexCurve<T> curve)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        var path = new SKPath();
        if (curve.IsEmpty) return path;

        bool needMove = true;

        foreach (var seg in curve)
        {
            if (seg.IsBezier)
            {
                var b = seg.AsBezier();
                if (needMove)
                {
                    path.MoveTo(F(b.Start.X), F(b.Start.Y));
                    needMove = false;
                }
                else
                {
                    var last = path.LastPoint;
                    if (MathF.Abs(last.X - F(b.Start.X)) > 0.001f || MathF.Abs(last.Y - F(b.Start.Y)) > 0.001f)
                        path.MoveTo(F(b.Start.X), F(b.Start.Y));
                }
                path.CubicTo(
                    F(b.C0.X), F(b.C0.Y),
                    F(b.C1.X), F(b.C1.Y),
                    F(b.End.X), F(b.End.Y));
            }
            else
            {
                var pts = seg.AsPoints();
                if (pts.Length == 0) continue;

                if (needMove)
                {
                    path.MoveTo(F(pts[0].X), F(pts[0].Y));
                    needMove = false;
                }
                else
                {
                    var last = path.LastPoint;
                    if (MathF.Abs(last.X - F(pts[0].X)) > 0.001f || MathF.Abs(last.Y - F(pts[0].Y)) > 0.001f)
                        path.MoveTo(F(pts[0].X), F(pts[0].Y));
                }

                for (int i = 1; i < pts.Length; i++)
                    path.LineTo(F(pts[i].X), F(pts[i].Y));
            }
        }

        return path;
    }

    // ════════════════════════════════════════════════
    //  SKCanvas.Draw* methods
    // ════════════════════════════════════════════════

    /// <summary>
    /// Draws a Polygon on the canvas.
    /// </summary>
    public static void DrawPolygon<T>(this SKCanvas canvas, Polygon<T> polygon, SKPaint paint)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        using var path = polygon.ToSKPath();
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a Polyline on the canvas.
    /// </summary>
    public static void DrawPolyline<T>(this SKCanvas canvas, in Polyline<T> polyline, SKPaint paint)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        using var path = polyline.ToSKPath();
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a cubic BezierCurve on the canvas.
    /// </summary>
    public static void DrawBezierCurve<T>(this SKCanvas canvas, in BezierCurve<T> bezier, SKPaint paint)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        using var path = bezier.ToSKPath();
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a ComplexCurve (mixed polyline + Bezier segments) on the canvas.
    /// </summary>
    public static void DrawComplexCurve<T>(this SKCanvas canvas, in ComplexCurve<T> curve, SKPaint paint)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        using var path = curve.ToSKPath();
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a Circle on the canvas.
    /// </summary>
    public static void DrawCircle<T>(this SKCanvas canvas, in Circle<T> circle, SKPaint paint)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        canvas.DrawCircle(F(circle.Center.X), F(circle.Center.Y), F(circle.Radius), paint);
    }

    /// <summary>
    /// Draws a Rectangle on the canvas.
    /// </summary>
    public static void DrawRectangle<T>(this SKCanvas canvas, in Rectangle<T> rect, SKPaint paint)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        canvas.DrawRect(F(rect.X), F(rect.Y), F(rect.Width), F(rect.Height), paint);
    }

    /// <summary>
    /// Draws a Segment (line between two points) on the canvas.
    /// </summary>
    public static void DrawSegment<T>(this SKCanvas canvas, in Segment<T> segment, SKPaint paint)
        where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        canvas.DrawLine(F(segment.Start.X), F(segment.Start.Y), F(segment.End.X), F(segment.End.Y), paint);
    }

    /// <summary>
    /// Converts any IFloatingPointIeee754 to float for SkiaSharp interop.
    /// </summary>
    private static float F<T>(T value) where T : IFloatingPointIeee754<T>
        => float.CreateTruncating(value);
}
