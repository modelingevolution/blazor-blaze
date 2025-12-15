using System.Net.WebSockets;
using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using SkiaSharp;

namespace BlazorBlaze.Server;

/// <summary>
/// WebSocket-based implementation of IRemoteCanvasV2 that encodes and streams
/// vector graphics to connected clients using Protocol v2 (multi-layer, stateful context).
///
/// Uses direct encoding - operations write immediately to buffer without deferred execution,
/// eliminating lambda allocations and array copies.
/// </summary>
public sealed class WebSocketRemoteCanvasV2 : IRemoteCanvasV2
{
    private readonly WebSocket _webSocket;
    private readonly byte[] _buffer;
    private readonly Dictionary<byte, LayerEncoderImpl> _layers = new();
    private readonly List<byte> _activeLayerIds = new();

    private ulong _frameId;

    public WebSocketRemoteCanvasV2(WebSocket webSocket, int bufferSize = 1024 * 1024)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _buffer = new byte[bufferSize];
    }

    public ulong FrameId => _frameId;

    public ILayerCanvas Layer(byte layerId)
    {
        if (!_layers.TryGetValue(layerId, out var layer))
        {
            layer = new LayerEncoderImpl(layerId, _buffer);
            _layers[layerId] = layer;
        }

        // Track that this layer was accessed in this frame
        if (!_activeLayerIds.Contains(layerId))
        {
            _activeLayerIds.Add(layerId);
        }

        return layer;
    }

    public ILayerCanvas this[byte layerId] => Layer(layerId);

    public void BeginFrame()
    {
        _frameId++;
        _activeLayerIds.Clear();

        // Reset all layers for new frame
        foreach (var layer in _layers.Values)
        {
            layer.Reset();
        }
    }

    public async ValueTask FlushAsync(CancellationToken ct = default)
    {
        if (_webSocket.State != WebSocketState.Open)
            return;

        if (_activeLayerIds.Count == 0)
            return;

        var span = _buffer.AsSpan();
        int offset = 0;

        // Write message header (client will sort layers for compositing)
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(span, _frameId, (byte)_activeLayerIds.Count);

        // Encode each layer - copy encoded data from layer buffers
        foreach (var layerId in _activeLayerIds)
        {
            var layer = _layers[layerId];
            offset += layer.CopyEncodedData(span.Slice(offset));
        }

        // Write end marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(span.Slice(offset));

        // Send via WebSocket
        await _webSocket.SendAsync(
            new ArraySegment<byte>(_buffer, 0, offset),
            WebSocketMessageType.Binary,
            true,
            ct);
    }

    public void Dispose()
    {
        _layers.Clear();
        _activeLayerIds.Clear();
    }

    /// <summary>
    /// Internal layer encoder that writes operations directly to buffer.
    /// No deferred execution - each operation immediately encodes its data.
    /// </summary>
    private sealed class LayerEncoderImpl : ILayerCanvas
    {
        private readonly byte[] _layerBuffer;
        private readonly byte _layerId;
        private FrameType _frameType = FrameType.Master;
        private int _operationCount;
        private int _dataOffset; // Current write position in buffer (after header space)

        // Reserve space for layer header at start of buffer
        private const int HeaderReserve = 16; // LayerId(1) + FrameType(1) + OpCount(varint up to 5) + padding

        public byte LayerId => _layerId;

        public LayerEncoderImpl(byte layerId, byte[] sharedBuffer)
        {
            _layerId = layerId;
            // Each layer gets its own section of a shared buffer or uses a dedicated buffer
            // For simplicity, use a dedicated buffer per layer
            // 256KB per layer to accommodate JPEG frames (Full HD JPEG ~30-80KB)
            _layerBuffer = new byte[256 * 1024];
            _dataOffset = HeaderReserve;
        }

        public void Reset()
        {
            _frameType = FrameType.Master;
            _operationCount = 0;
            _dataOffset = HeaderReserve;
        }

        /// <summary>
        /// Copies the encoded layer data (with header) to the destination buffer.
        /// </summary>
        public int CopyEncodedData(Span<byte> destination)
        {
            int offset = 0;

            switch (_frameType)
            {
                case FrameType.Master:
                    offset += VectorGraphicsEncoderV2.WriteLayerMaster(destination, _layerId, (uint)_operationCount);
                    // Copy operation data
                    var dataLength = _dataOffset - HeaderReserve;
                    _layerBuffer.AsSpan(HeaderReserve, dataLength).CopyTo(destination.Slice(offset));
                    offset += dataLength;
                    break;

                case FrameType.Remain:
                    offset += VectorGraphicsEncoderV2.WriteLayerRemain(destination, _layerId);
                    break;

                case FrameType.Clear:
                    offset += VectorGraphicsEncoderV2.WriteLayerClear(destination, _layerId);
                    break;
            }

            return offset;
        }

        private Span<byte> GetWriteSpan() => _layerBuffer.AsSpan(_dataOffset);

        #region Frame Type

        public void Master() => _frameType = FrameType.Master;
        public void Remain() => _frameType = FrameType.Remain;
        public void Clear() => _frameType = FrameType.Clear;

        #endregion

        #region Context State - Styling

        public void SetStroke(RgbColor color)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetStroke(GetWriteSpan(), color);
            _operationCount++;
        }

        public void SetFill(RgbColor color)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetFill(GetWriteSpan(), color);
            _operationCount++;
        }

        public void SetThickness(int width)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetThickness(GetWriteSpan(), width);
            _operationCount++;
        }

        public void SetFontSize(int size)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetFontSize(GetWriteSpan(), size);
            _operationCount++;
        }

        public void SetFontColor(RgbColor color)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetFontColor(GetWriteSpan(), color);
            _operationCount++;
        }

        #endregion

        #region Context State - Transforms

        public void Translate(float dx, float dy)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetOffset(GetWriteSpan(), dx, dy);
            _operationCount++;
        }

        public void Rotate(float degrees)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetRotation(GetWriteSpan(), degrees);
            _operationCount++;
        }

        public void Scale(float sx, float sy)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetScale(GetWriteSpan(), sx, sy);
            _operationCount++;
        }

        public void Skew(float kx, float ky)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetSkew(GetWriteSpan(), kx, ky);
            _operationCount++;
        }

        public void SetMatrix(SKMatrix matrix)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSetMatrix(GetWriteSpan(), matrix);
            _operationCount++;
        }

        #endregion

        #region Context Stack

        public void Save()
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteSaveContext(GetWriteSpan());
            _operationCount++;
        }

        public void Restore()
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteRestoreContext(GetWriteSpan());
            _operationCount++;
        }

        public void ResetContext()
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteResetContext(GetWriteSpan());
            _operationCount++;
        }

        #endregion

        #region Draw Operations

        public void DrawPolygon(ReadOnlySpan<SKPoint> points)
        {
            // Direct encoding - no array copy needed!
            _dataOffset += VectorGraphicsEncoderV2.WriteDrawPolygon(GetWriteSpan(), points);
            _operationCount++;
        }

        public void DrawText(string text, int x, int y)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteDrawText(GetWriteSpan(), text, x, y);
            _operationCount++;
        }

        public void DrawCircle(int centerX, int centerY, int radius)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteDrawCircle(GetWriteSpan(), centerX, centerY, radius);
            _operationCount++;
        }

        public void DrawRectangle(int x, int y, int width, int height)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteDrawRect(GetWriteSpan(), x, y, width, height);
            _operationCount++;
        }

        public void DrawLine(int x1, int y1, int x2, int y2)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteDrawLine(GetWriteSpan(), x1, y1, x2, y2);
            _operationCount++;
        }

        public void DrawJpeg(ReadOnlySpan<byte> jpegData, int x, int y, int width, int height)
        {
            _dataOffset += VectorGraphicsEncoderV2.WriteDrawJpeg(GetWriteSpan(), jpegData, x, y, width, height);
            _operationCount++;
        }

        #endregion
    }
}
