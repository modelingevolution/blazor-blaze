using BlazorBlaze.VectorGraphics;
using SkiaSharp;

namespace BlazorBlaze.Server;

/// <summary>
/// Server-side canvas interface for streaming vector graphics to connected clients using Protocol v2.
/// Features multi-layer support with keyframe compression for optimal bandwidth usage.
/// </summary>
public interface IRemoteCanvasV2 : IDisposable
{
    /// <summary>
    /// Current frame identifier, incremented on each BeginFrame() call.
    /// </summary>
    ulong FrameId { get; }

    /// <summary>
    /// Gets a layer canvas for drawing operations.
    /// </summary>
    /// <param name="layerId">Layer index (0 = bottom, higher = top)</param>
    ILayerCanvas Layer(byte layerId);

    /// <summary>
    /// Indexer shortcut for Layer().
    /// </summary>
    ILayerCanvas this[byte layerId] { get; }

    /// <summary>
    /// Begins a new frame, incrementing the frame ID.
    /// Call this at the start of each animation frame.
    /// </summary>
    void BeginFrame();

    /// <summary>
    /// Encodes and sends all layer operations to the client via WebSocket.
    /// Only layers that have been accessed since BeginFrame() are included.
    /// </summary>
    ValueTask FlushAsync(CancellationToken ct = default);
}

/// <summary>
/// Represents a single rendering layer with stateful context management.
/// Mirrors SkiaSharp's canvas API for familiar usage patterns.
/// </summary>
public interface ILayerCanvas
{
    /// <summary>
    /// The layer ID (z-order index).
    /// </summary>
    byte LayerId { get; }

    #region Layer Frame Type

    /// <summary>
    /// Sets this layer to Master mode - clears and redraws with operations that follow.
    /// This is the default mode when drawing operations are added.
    /// </summary>
    void Master();

    /// <summary>
    /// Sets this layer to Remain mode - keeps previous content unchanged.
    /// No operations are sent for this layer, saving bandwidth.
    /// </summary>
    void Remain();

    /// <summary>
    /// Sets this layer to Clear mode - clears to transparent with no redraw.
    /// Use when you want to hide a layer without drawing new content.
    /// </summary>
    void Clear();

    #endregion

    #region Context State - Styling

    /// <summary>
    /// Sets the stroke color for subsequent draw operations.
    /// </summary>
    void SetStroke(RgbColor color);

    /// <summary>
    /// Sets the fill color for subsequent draw operations.
    /// </summary>
    void SetFill(RgbColor color);

    /// <summary>
    /// Sets the stroke thickness in pixels.
    /// </summary>
    void SetThickness(int width);

    /// <summary>
    /// Sets the font size in pixels.
    /// </summary>
    void SetFontSize(int size);

    /// <summary>
    /// Sets the font color for text operations.
    /// </summary>
    void SetFontColor(RgbColor color);

    #endregion

    #region Context State - Transforms

    /// <summary>
    /// Sets the translation offset for subsequent draw operations.
    /// </summary>
    void Translate(float dx, float dy);

    /// <summary>
    /// Sets the rotation in degrees for subsequent draw operations.
    /// </summary>
    void Rotate(float degrees);

    /// <summary>
    /// Sets the scale factors for subsequent draw operations.
    /// </summary>
    void Scale(float sx, float sy);

    /// <summary>
    /// Sets the skew factors for subsequent draw operations.
    /// </summary>
    void Skew(float kx, float ky);

    /// <summary>
    /// Sets a full transformation matrix for subsequent draw operations.
    /// Takes precedence over individual transform properties.
    /// </summary>
    void SetMatrix(SKMatrix matrix);

    #endregion

    #region Context Stack

    /// <summary>
    /// Pushes the current context state onto a stack.
    /// Use with Restore() for hierarchical transforms.
    /// </summary>
    void Save();

    /// <summary>
    /// Pops and restores the most recently saved context state.
    /// </summary>
    void Restore();

    /// <summary>
    /// Resets the context to default values (black stroke, identity transform).
    /// </summary>
    void ResetContext();

    #endregion

    #region Draw Operations

    /// <summary>
    /// Draws a polygon using the current context state.
    /// </summary>
    void DrawPolygon(ReadOnlySpan<SKPoint> points);

    /// <summary>
    /// Draws text at the specified position using the current context state.
    /// </summary>
    void DrawText(string text, int x, int y);

    /// <summary>
    /// Draws a circle using the current context state.
    /// </summary>
    void DrawCircle(int centerX, int centerY, int radius);

    /// <summary>
    /// Draws a rectangle using the current context state.
    /// </summary>
    void DrawRectangle(int x, int y, int width, int height);

    /// <summary>
    /// Draws a line using the current context state.
    /// </summary>
    void DrawLine(int x1, int y1, int x2, int y2);

    #endregion
}
