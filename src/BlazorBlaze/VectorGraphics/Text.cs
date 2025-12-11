using ProtoBuf;

namespace BlazorBlaze.VectorGraphics;

[ProtoContract]
public class Text : IRenderItem
{
    [ProtoMember(1)]
    public string Content { get; init; }

    public void Render(ICanvas canvasChannel, DrawContext? context)
    {
        var offsetX = (int)(context?.Offset?.X ?? 0);
        var offsetY = (int)(context?.Offset?.Y ?? 0);
        var fontSize = context?.FontSize ?? 12;
        var fontColor = context?.FontColor ?? RgbColor.Black;

        canvasChannel.DrawText(Content, offsetX, offsetY, fontSize, fontColor);
    }

    public ushort Id => 1;
}