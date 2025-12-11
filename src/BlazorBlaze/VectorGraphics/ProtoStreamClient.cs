using System.Collections;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
[assembly:InternalsVisibleTo("ModelingEvolution.VideoStreaming.Tests")]

namespace BlazorBlaze.VectorGraphics;
// Uses websockets to communicate & .net protobuf serialization.
// Must be high performant, light-weight and native
// Transfer can be multiplexed with layerId.
// The stream contains Headers and sub-frames and payloads.
// The protcol is used to render vectors on a canvas. Each layer on canvas can be rendered independently.
// So each layer would be interested in data that only applies to this particular layer. The fps for rendering
// of each layer is idependent. So some layers might be refreshed quicker, other slower.
// In the stream we will find two types of data, Headers and SubHeaders. A header consist of FrameId, LayerId. 
// The FrameId is an ulong and it's first bit must be 0. If it is 1 this means it is a Subheader.
// Each Subheader consits of size and ushort type (required for serializer) and layerId.
// After Subheader goes payload that's the size defined in prv. subheader. 
// This subheader data can be then deserialized with protobuf serializer.
// For a given layer at the end of it's frame there is a marker Subframe with Size=0 and Type = ushort.Max.
public class ProtoStreamClient(ISerializer serializer, ILogger<ProtoStreamClient> logger) 
{
    public static readonly Frame Empty = new Frame(0,0,new List<object>(0));
    public readonly record struct Frame : IEnumerable<object>, IReadOnlyList<object>
    {
        public readonly ulong Number;
        internal readonly IList<object> Objects = null!;
        internal bool IsInitialized => Objects != null;
        public Frame(ulong nr, byte layerId)
        {
            this.Number = nr;
            LayerId = layerId;
            this.Objects = new List<object>();
        }
        public Frame(ulong nr, byte layerId, IList<object> o)
        {
            this.Number = nr;
            this.Objects = o;
            LayerId = layerId;
        }

        public int Count => Objects.Count;
        public byte LayerId { get; }

        public IEnumerator<object> GetEnumerator()
        {
            return Objects.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Objects).GetEnumerator();
        }

        public object this[int index] => Objects[index];
    }
    internal static SubHeader EOF(byte layerId = 0) => new SubHeader(0, ushort.MaxValue, layerId);
    
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    internal readonly record struct Header
    {
        // We expect that FrameId shall always have 0x0 on the first bit. If it is 1 this means that it is Subheader.
        [FieldOffset(0)]
        private readonly ulong _frameId;

        public ulong FrameId => _frameId >> 1;

        [FieldOffset(sizeof(ulong))]
        public readonly byte LayerId;

        
        public Header()
        {
            
        }

        public Header(ulong frameId, byte layerId)
        {
            _frameId = (frameId << 1);
            LayerId = layerId;
        }
    }
    
    [StructLayout(LayoutKind.Explicit, Pack=1)]
    internal readonly record struct SubHeader
    {
        // Subheader always have 1 on the first bit.
        [FieldOffset(0)]
        private readonly uint _size;

        public int Size => (int)(_size >> 1);
            
        [FieldOffset(sizeof(uint))]
        public readonly ushort Type;

        [FieldOffset(sizeof(uint) + sizeof(ushort))]
        public readonly byte LayerId;

        public bool IsEOF => Size == 0 && Type == ushort.MaxValue;

        public bool IsValid => (_size & 0x1) == 0x1;
        public SubHeader()
        {
                
        }

        public SubHeader(uint size, ushort type, byte layerId = 0)
        {
            this._size = (size << 1) + 0x1;
            this.Type = type;
            this.LayerId = layerId;
        }
    }

    private readonly Channel<Frame> _channel = Channel.CreateBounded<Frame>(new BoundedChannelOptions(1));
    private readonly ClientWebSocket _webSocket = new();
    private const int BufferSize = 32*1024*1024; // 8MB

    private readonly Frame[] _currentFrames = new Frame[255];

    private readonly CancellationTokenSource _cts = new();
        
    // Connect to the WebSocket server.
    public async Task ConnectAsync(Uri uri)
    {
        await _webSocket.ConnectAsync(uri, CancellationToken.None);
        logger.LogDebug("WebSocket connected!");
        _ = Task.Factory.StartNew(ReceiveLoop, TaskCreationOptions.LongRunning);
    }

    // This method constantly listens for incoming frames and processes them on the fly.
    private async Task ReceiveLoop()
    {
        try
        {
            var buffer = new byte[BufferSize];
            int offset = 0;
            var bps = new TransferWatch();
            var pw = new PeriodicConsoleWriter(TimeSpan.FromSeconds(2.5));
            while (_webSocket.State == WebSocketState.Open)
            {
                var bufferLength = buffer.Length - offset;
                var result =
                    await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, bufferLength),
                        CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    //Console.WriteLine("Received data: " + result.Count);
                    offset = ProcessFragment(buffer.AsSpan(offset,result.Count));
                    bps += result.Count;
                    if (offset > 0)
                        Buffer.BlockCopy(buffer, result.Count - offset, buffer, 0, offset);
                }
                pw.WriteLine($"Received speed: {bps.Value}/s");


                if (result.CloseStatus.HasValue)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);

            }
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "ReceiveLoop error");
        }
    }

    // Close the WebSocket connection.
    public async Task DisconnectAsync()
    {
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
    }

    

    enum State
    {
        ReadingStart, ReadingSubFrames
    }

    private static readonly int FRAME_HEADER_SIZE = Marshal.SizeOf<Header>();
    private static readonly int FRAME_SUBHEADER_SIZE = Marshal.SizeOf<SubHeader>();
    internal int ProcessFragment(in ReadOnlySpan<byte> buffer)
    {
        int offset = 0;
        var length = buffer.Length;

        while (offset < length)
        {
            // If we don't have enough data for at least a header or sub-header, we need more data.
            if (length - offset < 7)
            {
                // Not enough data to read the header/sub-header.
                return length - offset;
            }

            // Peek at the first bit of the next header or sub-header to determine if it's a frame header or sub-header.
            byte firstByte = buffer.Slice(offset, 1)[0];
            

            if ((firstByte & 0x1) == 0)
            {
                // This is a frame header (first bit is 0).
                if (length - offset < FRAME_HEADER_SIZE)
                {
                    // Not enough data to read the full header (FrameId + LayerId).
                    return length - offset;
                }

                // Read the header
                var header = MemoryMarshal.Read<Header>(buffer.Slice(offset, FRAME_HEADER_SIZE));
                offset += FRAME_HEADER_SIZE;

                // Initialize the current frame if it's a new frame.
                var f = _currentFrames[header.LayerId];
                if ((!f.IsInitialized) || f.Number != header.FrameId)
                {
                    _currentFrames[header.LayerId] = new Frame(header.FrameId, header.LayerId);
                }
                else
                {
                    // this can happen because same frame can be written from many parts.
                    //throw new InvalidOperationException($"Duplicated frame detected! FrameId: {header.FrameId}, LayerId: {header.LayerId}");
                }
            }
            else
            {
                // This is a sub-header (first bit is 1).
                if (length - offset < FRAME_SUBHEADER_SIZE)
                {
                    // Not enough data to read the full sub-header.
                    return length - offset;
                }

                // Read the sub-header
                var subHeader = MemoryMarshal.Read<SubHeader>(buffer.Slice(offset));
                if (!subHeader.IsValid)
                    throw new InvalidOperationException("SubHeader is not valid");
                
                offset += FRAME_SUBHEADER_SIZE;

                // Check if the sub-header is EOF (End of Frame)
                if (subHeader.IsEOF)
                {
                    // End of frame, push the frame to the corresponding layer's channel
                    _channel.Writer.TryWrite(_currentFrames[subHeader.LayerId]);
                    continue; // Move to the next frame or sub-header
                }

                // Ensure there's enough data for the payload
                if (length - offset < subHeader.Size)
                {
                    // Not enough data to read the payload
                    return length - offset + FRAME_SUBHEADER_SIZE;
                }

                // Extract the payload based on the sub-header's size
                var payload = buffer.Slice(offset, subHeader.Size);
                offset += subHeader.Size;

                // Deserialize the payload using the provided serializer
                var deserializedObject = serializer.Deserialize(ref payload, subHeader.Type);
                _currentFrames[subHeader.LayerId].Objects.Add(deserializedObject);
            }
        }

        return 0; // Fully processed the fragment
    }
    

    public IAsyncEnumerable<Frame> Read(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}