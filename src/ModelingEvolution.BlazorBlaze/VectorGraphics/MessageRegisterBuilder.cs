using System.Collections.Frozen;

namespace ModelingEvolution.BlazorBlaze.VectorGraphics;

public class MessageRegisterBuilder
{
    private readonly Dictionary<ushort, Type> _types = new();
    public static MessageRegisterBuilder Create() => new MessageRegisterBuilder();
    public MessageRegisterBuilder With(ushort type, Type t)
    {
        _types.Add(type, t);
        return this;
    }

    public TryGetValue<ushort, Type> Build()
    {
        return _types.ToFrozenDictionary().TryGetValue!;
    }
}