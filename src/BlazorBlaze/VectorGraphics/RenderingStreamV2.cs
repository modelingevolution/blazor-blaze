using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using BlazorBlaze.VectorGraphics.Protocol;
using ModelingEvolution;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// Protocol v2 rendering stream that handles WebSocket connection and decoding
/// with multi-layer support and stateful context management.
/// </summary>
public class RenderingStreamV2 : IRenderingStream
{
    private readonly VectorGraphicsDecoderV2 _decoder;
    private readonly RenderingCallbackV2 _callback;
    private readonly LayerManager _layerManager;
    private readonly ILogger _logger;
    private readonly int _maxBufferSize;
    private readonly object _sync = new();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    // Transfer rate tracking (sliding window of 1 second)
    private readonly Queue<(long timestamp, int bytes)> _bytesWindow = new();
    private long _currentWindowBytes;
    private readonly object _bytesLock = new();

    public RenderingStreamV2(
        int width,
        int height,
        ILoggerFactory loggerFactory,
        int maxBufferSize = 8 * 1024 * 1024)
    {
        _layerManager = new LayerManager(width, height);
        _callback = new RenderingCallbackV2(_layerManager);
        _decoder = new VectorGraphicsDecoderV2();
        _logger = loggerFactory.CreateLogger<RenderingStreamV2>();
        _maxBufferSize = maxBufferSize;
    }

    public bool IsConnected { get; private set; }
    public ulong Frame { get; private set; }
    public float Fps { get; private set; }
    public string? Error { get; private set; }
    public Bytes TransferRate { get; private set; }

    public async Task ConnectAsync(Uri uri, CancellationToken ct = default)
    {
        if (IsConnected)
            throw new InvalidOperationException("Already connected");

        _socket = new ClientWebSocket();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await _socket.ConnectAsync(uri, _cts.Token);
            IsConnected = true;
            Error = null;
            _receiveTask = Task.Factory.StartNew(ReceiveLoop, TaskCreationOptions.LongRunning);
            _logger.LogDebug("RenderingStreamV2 connected to {Uri}", uri);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _logger.LogError(ex, "Failed to connect to {Uri}", uri);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected)
            return;

        _cts?.Cancel();

        if (_socket?.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error during close");
            }
        }

        IsConnected = false;
        _logger.LogDebug("RenderingStreamV2 disconnected");
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[_maxBufferSize];
        int offset = 0;
        var fpsWatch = new FpsWatch();

        try
        {
            while (_socket!.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
                var result = await _socket.ReceiveAsync(segment, _cts.Token);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Track bytes for transfer rate calculation
                    TrackBytes(result.Count);

                    var totalLength = offset + result.Count;
                    var data = buffer.AsSpan(0, totalLength);

                    while (data.Length > 0)
                    {
                        DecodeResultV2 decoded;
                        lock (_sync)
                        {
                            decoded = _decoder.Decode(data, _callback);
                        }

                        if (decoded.Success && decoded.FrameId.HasValue)
                        {
                            Frame = decoded.FrameId.Value;
                            Fps = (float)fpsWatch++.Value;

                            if (decoded.BytesConsumed < data.Length)
                            {
                                data = data.Slice(decoded.BytesConsumed);
                            }
                            else
                            {
                                data = Span<byte>.Empty;
                            }
                        }
                        else if (decoded.BytesConsumed > 0)
                        {
                            data = data.Slice(decoded.BytesConsumed);
                        }
                        else
                        {
                            // Need more data
                            break;
                        }
                    }

                    // Copy remaining data to start of buffer
                    if (data.Length > 0)
                    {
                        data.CopyTo(buffer);
                        offset = data.Length;
                    }
                    else
                    {
                        offset = 0;
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disconnect
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _logger.LogError(ex, "RenderingStreamV2 receive error");
        }
        finally
        {
            IsConnected = false;
        }
    }

    private void TrackBytes(int bytesReceived)
    {
        var now = Environment.TickCount64;
        const long windowMs = 1000; // 1 second window

        lock (_bytesLock)
        {
            // Add new entry
            _bytesWindow.Enqueue((now, bytesReceived));
            _currentWindowBytes += bytesReceived;

            // Remove entries older than the window
            while (_bytesWindow.Count > 0 && now - _bytesWindow.Peek().timestamp > windowMs)
            {
                var old = _bytesWindow.Dequeue();
                _currentWindowBytes -= old.bytes;
            }

            // Update transfer rate (bytes per second)
            TransferRate = _currentWindowBytes;
        }
    }

    public void Render(SKCanvas canvas)
    {
        lock (_sync)
        {
            _layerManager.Composite(canvas);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _socket?.Dispose();
        _cts?.Dispose();
        _layerManager.Dispose();
    }
}
