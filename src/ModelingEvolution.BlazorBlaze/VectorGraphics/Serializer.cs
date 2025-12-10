namespace ModelingEvolution.BlazorBlaze.VectorGraphics;

/// <summary>
/// Uses protobuf-net for serialization
/// </summary>
public class Serializer(TryGetValue<ushort, Type> register) : ISerializer
{
    public object Deserialize(ref ReadOnlySpan<byte> data, ushort type)
    {
        if (!register(type, out var targetType))
        {
            throw new ArgumentException($"Unknown type: {type}", nameof(type));
        }
            
        return ProtoBuf.Serializer.NonGeneric.Deserialize(targetType, data);
    }
}