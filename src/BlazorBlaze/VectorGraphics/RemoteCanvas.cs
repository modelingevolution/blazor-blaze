using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics;

public interface IRemoteCanvasStreamPool
{
    ICanvas GetCanvas(string streamId);
}


public class RemoteCanvasStreamPool(ILoggerFactory loggerFactory) : IRemoteCanvasStreamPool
{
    private record Item(ICanvas Canvas, BufferWriter Writer);
    private readonly ConcurrentDictionary<string, Item> _items = new ConcurrentDictionary<string, Item>();
    public ICanvas GetCanvas(string streamId)
    {
        return Get(streamId).Canvas;
    }

    private Item Get(string streamId)
    {
        return _items.GetOrAdd(streamId, _ =>
        {
            var bufferWriter = new BufferWriter(loggerFactory);
            var item = new Item(new RemoteCanvas(bufferWriter.Begin, bufferWriter.Push, bufferWriter.End),
                bufferWriter);

            return item;
        });
    }
    public IWebSocketSink JoinWebSocket(string streamId, WebSocket ws) => Get(streamId).Writer.Join(ws);
}

public interface IWebSocketSink : IDisposable
{
    Task WaitClose(CancellationToken token = default);
}

// Data can be written in chunks. We expect that a chunk cannot be bigger than _maxChunk.
// We return Memory only that is required by a hint, no more.
// A chunk of data needs to be in one-piece. If it happens to be that the chank is at the end of the
// buffer and we want to write more data, than we will move it to the beginning of the buffer.
// The buffer acts as a cyclic chunk buffer.
// When need chunk will be written, SlideChunk will be executed. Slide chunk acts as a if the SlidingBufferWriter looks like a new fresh buffer,
// however under the hood, it just takes next chunk.
public class BufferWriter(ILoggerFactory loggerFactory)
{
    readonly record struct Msg(MsgType Type, Memory<byte> Data);
    private readonly SlidingBufferWriter _buffer = new();

    private readonly BroadcastBlock<Msg> _data = new BroadcastBlock<Msg>(null);
    enum MsgType : byte
    {
        Start = 0x0, Obj= 0x1, End = 0x2,
    }


    class SinkBlock : IWebSocketSink
    {
        private readonly ActionBlock<Msg> _block;
        private readonly WebSocket _socket;
        private IDisposable? _link;
        private bool _started;
        private readonly ILogger<SinkBlock> _logger;
        private readonly AsyncManualResetEvent _closed = new AsyncManualResetEvent(false);
        public SinkBlock(WebSocket socket, ILogger<SinkBlock> logger)
        {
            _block = new ActionBlock<Msg>(OnReceive);
            this._socket = socket;
            _logger = logger;

        }

        public ITargetBlock<Msg> Block => _block;

        public IWebSocketSink LinkFrom(ISourceBlock<Msg> src)
        {
            _link = src.LinkTo(_block);
            return this;
        }
        private async Task OnReceive(Msg msg)
        {
            if (_socket.CloseStatus.HasValue)
            {
                _link?.Dispose();
                _closed.Set();
            }
            else
            {
                if (RequireNxMessage(msg)) return;
                try
                {
                    //_logger.LogInformation($"Sending: {msg.Type}, {msg.Data.Length}B");
                    await _socket.SendAsync(msg.Data, WebSocketMessageType.Binary, true, default);
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.LogInformation("WebSocket Connection Closed");
                }
                catch(Exception ex)
                {
                    _logger.LogWarning(ex,"WebSocket Connection Closed");
                    _link?.Dispose();
                    _link = null;
                    _closed.Set();
                }
            }
        }

        private bool RequireNxMessage(Msg msg)
        {
            if (_started) return false;
            if (msg.Type == MsgType.Start)
                _started = true;
            else return true;

            return false;
        }

        public void Dispose()
        {
            _socket.Dispose();
            _link?.Dispose();

        }

        public async Task WaitClose(CancellationToken token = default)
        {
            await this._closed.WaitAsync(token);
        }
    }
    public IWebSocketSink Join(WebSocket ws)
    {
        return new SinkBlock(ws, loggerFactory.CreateLogger<SinkBlock>()).LinkFrom(_data);
    }
    public void Push(IRenderOp obj, byte layerId)
    {

        _buffer.WriteFramePayload(obj.Id, layerId, obj);
        var m = _buffer.WrittenMemory;
        _data.Post(new Msg(MsgType.Obj,m));
        _buffer.SlideChunk();
        //Debug.WriteLine($"Push layer {layerId} with {obj.Id}, {m.Length}B");
    }

    public void End(byte layerId)
    {

        _buffer.WriteFrameEnd(layerId);
        var m = _buffer.WrittenMemory;
        _data.Post(new Msg(MsgType.End, m));
        _buffer.SlideChunk();
        //Debug.WriteLine($"End: {layerId}, {m.Length}B");
    }

    public void Begin(ulong obj, byte layerId)
    {

        _buffer.WriteFrameNumber(obj,layerId);
        var m = _buffer.WrittenMemory;
        _data.Post(new Msg(MsgType.Start, m));
        _buffer.SlideChunk();
        //Debug.WriteLine($"Begin: {obj}, layer: {layerId}, {m.Length}B");
    }
}

public class RemoteCanvas(Action<ulong, byte> onBegin, Action<IRenderOp, byte> onPush, Action<byte> onEnd) : ICanvas
{
    public object Sync { get; } = new object();

    public void DrawRectangle(System.Drawing.Rectangle rect, RgbColor? color, byte? layerId)
    {
        var points = new SKPoint[]
        {
            new(rect.X, rect.Y),
            new(rect.X + rect.Width, rect.Y),
            new(rect.X + rect.Width, rect.Y + rect.Height),
            new(rect.X, rect.Y + rect.Height),
        };
        DrawPolygon(points, color, 1, layerId);
    }

    public void DrawPolygon(ReadOnlySpan<SKPoint> points, RgbColor? color = null, int width = 1, byte? layerId = null)
    {
        var polygon = Polygon.From(points);

        var renderOp = new Draw<Polygon>
        {
            Value = polygon,
            Context = new DrawContext
            {
                 Stroke = color ?? RgbColor.Black,
                 Thickness = (ushort)width,
            }
        };
        onPush(renderOp, layerId ?? LayerId);
    }

    public void DrawPolygon(ReadOnlySpan<SKPoint> points, DrawContext? context, byte? layerId = null)
    {
        var ctx = context ?? new DrawContext();
        DrawPolygon(points, ctx.Stroke, ctx.Thickness, layerId);
    }

    public byte LayerId { get; set; } = 0x0;
    public void End(byte? layerId = null) => onEnd(layerId ?? LayerId);
    public void Begin(ulong frameNr, byte? layerId = null) => onBegin(frameNr, layerId ?? LayerId);

    public void DrawText(string text, int x = 0, int y = 0, int size = 12, RgbColor? color = null, byte? layerId = null)
    {
        var renderOp = new Draw<Text>
        {
            Value = new Text { Content = text },
            Context = new DrawContext
            {
                FontSize = (ushort)size,
                FontColor = color ?? RgbColor.Black,
                Offset = new SKPoint(x, y)
            }
        };
        onPush(renderOp, layerId ?? LayerId);
    }


}
