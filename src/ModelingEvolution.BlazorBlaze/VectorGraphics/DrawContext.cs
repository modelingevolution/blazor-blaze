using ProtoBuf;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.VectorGraphics;

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
}