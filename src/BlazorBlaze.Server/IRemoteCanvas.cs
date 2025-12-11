using System.Drawing;
using BlazorBlaze.VectorGraphics;
using SkiaSharp;

namespace BlazorBlaze.Server;

/// <summary>
/// Server-side canvas interface for streaming vector graphics to connected clients.
/// Operations are buffered until FlushAsync is called.
/// </summary>
public interface IRemoteCanvas : IDisposable
{
    /// <summary>
    /// Current frame identifier, incremented on each Begin() call.
    /// </summary>
    ulong FrameId { get; }

    /// <summary>
    /// Current layer ID for z-ordering of graphics.
    /// </summary>
    byte LayerId { get; set; }

    /// <summary>
    /// Begins a new frame, clearing the operation buffer.
    /// </summary>
    /// <param name="layerId">Optional layer ID for z-ordering.</param>
    void Begin(byte? layerId = null);

    /// <summary>
    /// Draws a filled or stroked polygon.
    /// </summary>
    void DrawPolygon(ReadOnlySpan<SKPoint> points, DrawContext? context = null);

    /// <summary>
    /// Draws a rectangle.
    /// </summary>
    void DrawRectangle(Rectangle rect, DrawContext? context = null);

    /// <summary>
    /// Draws a circle.
    /// </summary>
    void DrawCircle(int centerX, int centerY, int radius, DrawContext? context = null);

    /// <summary>
    /// Draws text at the specified position.
    /// </summary>
    void DrawText(string text, int x, int y, DrawContext? context = null);

    /// <summary>
    /// Encodes and sends all buffered operations to the client.
    /// </summary>
    ValueTask FlushAsync(CancellationToken ct = default);
}
