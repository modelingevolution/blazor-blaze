using System.Buffers;
using System.Runtime.InteropServices;

namespace BlazorBlaze.VectorGraphics;

public static class FrameWriterExtensions
{
    public static MemoryBufferWriter GetWriter(this ref Memory<byte> mem)
    {
        return new MemoryBufferWriter(ref mem);
    }
    public static void WriteFrameNumber(this IBufferWriter<byte> buffer, ulong frame, byte layer)
    {
        ProtoStreamClient.Header h = new ProtoStreamClient.Header(frame,layer);
        MemoryMarshal.Write(buffer.GetSpan(HEADER_SIZE), h);
        buffer.Advance(HEADER_SIZE);
    }

    public static void WriteFrameEnd(this IBufferWriter<byte> buffer, byte layer)
    {
        var destination = buffer.GetSpan(SUB_HEADER_SIZE).Slice(0,SUB_HEADER_SIZE);
        MemoryMarshal.Write( destination,ProtoStreamClient.EOF(layer));
        buffer.Advance(SUB_HEADER_SIZE);
    }
    private static readonly int SUB_HEADER_SIZE = Marshal.SizeOf<ProtoStreamClient.SubHeader>();
    private static readonly int HEADER_SIZE = Marshal.SizeOf<ProtoStreamClient.Header>();
    public static void WriteFramePayload<T>(this IBufferWriter<byte> buffer, ushort type, byte layerId, T value)
    {
        var subFrameMem = buffer.GetMemory(8*1024);
        
        var header = subFrameMem
            .Slice(0,SUB_HEADER_SIZE);

        var msgMem = subFrameMem.Slice(SUB_HEADER_SIZE);
        var msgWriter = msgMem.GetWriter();
        ProtoBuf.Serializer.Serialize(msgWriter, value);
            
        var h = new ProtoStreamClient.SubHeader((uint)msgWriter.WrittenMemory, type, layerId);
        MemoryMarshal.Write(header.Span,h);
        buffer.Advance(msgWriter.WrittenMemory + SUB_HEADER_SIZE);
    }
        
}