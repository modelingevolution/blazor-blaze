using ProtoBuf;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics;

[ProtoContract]
public readonly record struct DrawContext
{
    [ProtoMember(1)]
    public SKPoint? Offset { get; init; }

    [ProtoMember(2)]
    public RgbColor? Fill { get; init; }

    [ProtoMember(3)]
    public RgbColor? Stroke { get; init; }

    [ProtoMember(4)]
    public ushort Thickness { get; init; }

    [ProtoMember(5)]
    public ushort FontSize { get; init; }

    [ProtoMember(6)]
    public RgbColor? FontColor { get; init; }

    [ProtoMember(7)]
    public float? Rotation { get; init; }

    [ProtoMember(8)]
    public SKPoint? Scale { get; init; }

    [ProtoMember(9)]
    public SKPoint? Skew { get; init; }

    public bool HasTransform => Offset.HasValue || Rotation.HasValue || Scale.HasValue || Skew.HasValue;

    public SKMatrix ToMatrix()
    {
        if (!HasTransform)
            return SKMatrix.Identity;

        var matrix = SKMatrix.Identity;

        if (Scale.HasValue)
            matrix = matrix.PreConcat(SKMatrix.CreateScale(Scale.Value.X, Scale.Value.Y));

        if (Rotation.HasValue)
            matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(Rotation.Value));

        if (Skew.HasValue)
            matrix = matrix.PreConcat(SKMatrix.CreateSkew(Skew.Value.X, Skew.Value.Y));

        if (Offset.HasValue)
            matrix = matrix.PreConcat(SKMatrix.CreateTranslation(Offset.Value.X, Offset.Value.Y));

        return matrix;
    }
}