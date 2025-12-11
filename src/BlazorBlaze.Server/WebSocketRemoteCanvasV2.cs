using System.Net.WebSockets;
using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using SkiaSharp;

namespace BlazorBlaze.Server;

/// <summary>
/// WebSocket-based implementation of IRemoteCanvasV2 that encodes and streams
/// vector graphics to connected clients using Protocol v2 (multi-layer, stateful context).
/// </summary>
public sealed class WebSocketRemoteCanvasV2 : IRemoteCanvasV2
{
    private readonly WebSocket _webSocket;
    private readonly byte[] _buffer;
    private readonly Dictionary<byte, LayerCanvasImpl> _layers = new();
    private readonly List<byte> _activeLayerIds = new();

    private ulong _frameId;

    public WebSocketRemoteCanvasV2(WebSocket webSocket, int bufferSize = 512 * 1024)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _buffer = new byte[bufferSize];
    }

    public ulong FrameId => _frameId;

    public ILayerCanvas Layer(byte layerId)
    {
        if (!_layers.TryGetValue(layerId, out var layer))
        {
            layer = new LayerCanvasImpl(layerId);
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

        // Sort layer IDs to ensure consistent ordering
        _activeLayerIds.Sort();

        // Write message header
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(span, _frameId, (byte)_activeLayerIds.Count);

        // Encode each layer
        foreach (var layerId in _activeLayerIds)
        {
            var layer = _layers[layerId];
            offset += layer.Encode(span.Slice(offset));
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
    /// Internal layer canvas implementation that tracks operations for encoding.
    /// </summary>
    private sealed class LayerCanvasImpl : ILayerCanvas
    {
        private readonly List<Action<Span<byte>, CountingWriter>> _operations = new();
        private FrameType _frameType = FrameType.Master;

        public byte LayerId { get; }

        public LayerCanvasImpl(byte layerId)
        {
            LayerId = layerId;
        }

        public void Reset()
        {
            _operations.Clear();
            _frameType = FrameType.Master;
        }

        public int Encode(Span<byte> buffer)
        {
            int offset = 0;

            switch (_frameType)
            {
                case FrameType.Master:
                    offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer, LayerId, (uint)_operations.Count);
                    var writer = new CountingWriter { Offset = offset };
                    foreach (var op in _operations)
                    {
                        op(buffer, writer);
                    }
                    offset = writer.Offset;
                    break;

                case FrameType.Remain:
                    offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer, LayerId);
                    break;

                case FrameType.Clear:
                    offset += VectorGraphicsEncoderV2.WriteLayerClear(buffer, LayerId);
                    break;
            }

            return offset;
        }

        #region Frame Type

        public void Master() => _frameType = FrameType.Master;
        public void Remain() => _frameType = FrameType.Remain;
        public void Clear() => _frameType = FrameType.Clear;

        #endregion

        #region Context State - Styling

        public void SetStroke(RgbColor color)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.Slice(writer.Offset), color);
            });
        }

        public void SetFill(RgbColor color)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetFill(buffer.Slice(writer.Offset), color);
            });
        }

        public void SetThickness(int width)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetThickness(buffer.Slice(writer.Offset), width);
            });
        }

        public void SetFontSize(int size)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetFontSize(buffer.Slice(writer.Offset), size);
            });
        }

        public void SetFontColor(RgbColor color)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetFontColor(buffer.Slice(writer.Offset), color);
            });
        }

        #endregion

        #region Context State - Transforms

        public void Translate(float dx, float dy)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetOffset(buffer.Slice(writer.Offset), dx, dy);
            });
        }

        public void Rotate(float degrees)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetRotation(buffer.Slice(writer.Offset), degrees);
            });
        }

        public void Scale(float sx, float sy)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetScale(buffer.Slice(writer.Offset), sx, sy);
            });
        }

        public void Skew(float kx, float ky)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetSkew(buffer.Slice(writer.Offset), kx, ky);
            });
        }

        public void SetMatrix(SKMatrix matrix)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSetMatrix(buffer.Slice(writer.Offset), matrix);
            });
        }

        #endregion

        #region Context Stack

        public void Save()
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteSaveContext(buffer.Slice(writer.Offset));
            });
        }

        public void Restore()
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteRestoreContext(buffer.Slice(writer.Offset));
            });
        }

        public void ResetContext()
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteResetContext(buffer.Slice(writer.Offset));
            });
        }

        #endregion

        #region Draw Operations

        public void DrawPolygon(ReadOnlySpan<SKPoint> points)
        {
            // Need to copy points since Span can't be captured in closure
            var pointsCopy = points.ToArray();
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.Slice(writer.Offset), pointsCopy);
            });
        }

        public void DrawText(string text, int x, int y)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteDrawText(buffer.Slice(writer.Offset), text, x, y);
            });
        }

        public void DrawCircle(int centerX, int centerY, int radius)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.Slice(writer.Offset), centerX, centerY, radius);
            });
        }

        public void DrawRectangle(int x, int y, int width, int height)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteDrawRect(buffer.Slice(writer.Offset), x, y, width, height);
            });
        }

        public void DrawLine(int x1, int y1, int x2, int y2)
        {
            _operations.Add((buffer, writer) =>
            {
                writer.Offset += VectorGraphicsEncoderV2.WriteDrawLine(buffer.Slice(writer.Offset), x1, y1, x2, y2);
            });
        }

        #endregion
    }

    /// <summary>
    /// Helper class to track offset through operation callbacks.
    /// </summary>
    private sealed class CountingWriter
    {
        public int Offset;
    }
}
