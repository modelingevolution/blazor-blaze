using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.InteropServices;

namespace BlazorBlaze.VectorGraphics;

public class WebSocketFrameWriter(WebSocket socket)
{
    private readonly ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>(512 * 1024);
    private MemoryBufferWriter _msgBuffer;
    public async Task WriteFrameNumber(ulong frame, byte layer)
    {
        buffer.Clear();
        var span = buffer.GetMemory(sizeof(ulong));
        MemoryMarshal.Write(span.Span, frame);
        buffer.Advance(sizeof(long));
        await socket.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Binary, false, CancellationToken.None);
    }
    public async Task WriteFrameEnd()
    {
        buffer.Clear();
        var destination = buffer.GetSpan(SubHeaderSize).Slice(0, SubHeaderSize);
        MemoryMarshal.Write(destination, ProtoStreamClient.EOF(0));
        buffer.Advance(SubHeaderSize);
        await socket.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Binary, true, CancellationToken.None);
    }
    public async Task WriteFramePayload<T>(ushort type, T value)
    {
        buffer.Clear();
        var header = buffer.GetSpan(SubHeaderSize)
            .Slice(0, SubHeaderSize);

        var msgMem = buffer.GetMemory(0).Slice(SubHeaderSize);
        _msgBuffer  = _msgBuffer?.Init(ref msgMem) ?? msgMem.GetWriter();
        ProtoBuf.Serializer.Serialize(_msgBuffer, value);

        var h = new ProtoStreamClient.SubHeader((uint)_msgBuffer.WrittenMemory, type);
        MemoryMarshal.Write(header, h);
        buffer.Advance(_msgBuffer.WrittenMemory + SubHeaderSize);
        await socket.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Binary, true, CancellationToken.None);
    }
    private static readonly int SubHeaderSize = Marshal.SizeOf<ProtoStreamClient.SubHeader>();
}