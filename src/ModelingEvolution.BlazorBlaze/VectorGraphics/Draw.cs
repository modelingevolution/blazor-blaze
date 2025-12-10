using ProtoBuf;

namespace ModelingEvolution.BlazorBlaze.VectorGraphics;

[ProtoContract]
public class Draw<TObject> : IRenderOp, IDisposable
    where TObject: IRenderItem
{
    [ProtoMember(1)]
    public TObject Value { get; init; }

    [ProtoMember(2)]
    public DrawContext? Context { get; init; }

    /// <summary>
    /// When true (default), Dispose() will dispose the inner Value if it's IDisposable.
    /// Set to false when reusing Draw objects across frames to prevent premature disposal.
    /// </summary>
    public bool OwnsValue { get; init; } = true;

    //[ProtoMember(3)]
    //public byte ContextId { get; set; }

    public void Render(ICanvas canvasChannel)
    {
        Value.Render(canvasChannel,Context);
    }

    public ushort Id => Value.Id;
    public void Dispose()
    {
        if(OwnsValue && Value is IDisposable d) d.Dispose();
    }
}