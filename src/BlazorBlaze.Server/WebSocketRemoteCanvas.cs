using System.Drawing;
using System.Net.WebSockets;
using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using SkiaSharp;

namespace BlazorBlaze.Server;

/// <summary>
/// WebSocket-based implementation of IRemoteCanvas that encodes and streams
/// vector graphics to connected clients using the binary protocol.
/// </summary>
public sealed class WebSocketRemoteCanvas : IRemoteCanvas
{
    private readonly WebSocket _webSocket;
    private readonly byte[] _buffer;
    private readonly List<(DrawOperation Op, object Data, DrawContext? Context)> _operations = new();

    private ulong _frameId;
    private byte _layerId;

    private enum DrawOperation
    {
        Polygon,
        Rectangle,
        Circle,
        Text
    }

    public WebSocketRemoteCanvas(WebSocket webSocket, int bufferSize = 512 * 1024)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _buffer = new byte[bufferSize];
    }

    public ulong FrameId => _frameId;

    public byte LayerId
    {
        get => _layerId;
        set => _layerId = value;
    }

    public void Begin(byte? layerId = null)
    {
        _frameId++;
        _layerId = layerId ?? 0;
        _operations.Clear();
    }

    public void DrawPolygon(ReadOnlySpan<SKPoint> points, DrawContext? context = null)
    {
        // Copy points to array since we need to store them
        var pointsCopy = points.ToArray();
        _operations.Add((DrawOperation.Polygon, pointsCopy, context));
    }

    public void DrawRectangle(Rectangle rect, DrawContext? context = null)
    {
        _operations.Add((DrawOperation.Rectangle, rect, context));
    }

    public void DrawCircle(int centerX, int centerY, int radius, DrawContext? context = null)
    {
        _operations.Add((DrawOperation.Circle, (centerX, centerY, radius), context));
    }

    public void DrawText(string text, int x, int y, DrawContext? context = null)
    {
        _operations.Add((DrawOperation.Text, (text, x, y), context));
    }

    public async ValueTask FlushAsync(CancellationToken ct = default)
    {
        if (_webSocket.State != WebSocketState.Open)
            return;

        var span = _buffer.AsSpan();
        int offset = 0;

        // Frame header
        offset += VectorGraphicsEncoder.WriteFrameHeader(span, _frameId, _layerId);

        // Object count
        offset += VectorGraphicsEncoder.WriteObjectCount(span.Slice(offset), (uint)_operations.Count);

        // Encode each operation
        foreach (var (op, data, context) in _operations)
        {
            switch (op)
            {
                case DrawOperation.Polygon:
                    var points = (SKPoint[])data;
                    offset += VectorGraphicsEncoder.WritePolygon(span.Slice(offset), points, context ?? default);
                    break;

                case DrawOperation.Rectangle:
                    var rect = (Rectangle)data;
                    offset += VectorGraphicsEncoder.WriteRectangle(span.Slice(offset), rect.X, rect.Y, rect.Width, rect.Height, context ?? default);
                    break;

                case DrawOperation.Circle:
                    var (cx, cy, r) = ((int, int, int))data;
                    offset += VectorGraphicsEncoder.WriteCircle(span.Slice(offset), cx, cy, r, context ?? default);
                    break;

                case DrawOperation.Text:
                    var (text, x, y) = ((string, int, int))data;
                    offset += VectorGraphicsEncoder.WriteText(span.Slice(offset), text, x, y, context ?? default);
                    break;
            }
        }

        // End marker
        offset += VectorGraphicsEncoder.WriteEndMarker(span.Slice(offset));

        // Send via WebSocket
        await _webSocket.SendAsync(
            new ArraySegment<byte>(_buffer, 0, offset),
            WebSocketMessageType.Binary,
            true,
            ct);
    }

    public void Dispose()
    {
        _operations.Clear();
    }
}
