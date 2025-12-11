using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Decoder callback implementation that renders operations to a LayerManager.
/// Implements the client-side rendering with transform support.
/// </summary>
public class RenderingCallbackV2 : IDecoderCallbackV2
{
    private readonly LayerManager _layerManager;
    private ulong _currentFrameId;
    private byte _currentLayerId;
    private FrameType _currentFrameType;

    // Thread-local SKPath for reuse
    [ThreadStatic]
    private static SKPath? _reusablePath;

    public RenderingCallbackV2(LayerManager layerManager)
    {
        _layerManager = layerManager;
    }

    /// <summary>
    /// Gets the current frame ID after decoding.
    /// </summary>
    public ulong CurrentFrameId => _currentFrameId;

    public void OnFrameStart(ulong frameId, byte layerCount)
    {
        _currentFrameId = frameId;
    }

    public void OnLayerStart(byte layerId, FrameType frameType)
    {
        _currentLayerId = layerId;
        _currentFrameType = frameType;

        switch (frameType)
        {
            case FrameType.Master:
                // Clear layer and prepare for new content
                _layerManager.ClearLayer(layerId);
                break;

            case FrameType.Clear:
                // Clear layer to transparent
                _layerManager.ClearLayer(layerId);
                break;

            case FrameType.Remain:
                // Keep previous content - nothing to do
                break;
        }
    }

    public void OnLayerEnd(byte layerId)
    {
        // Layer is complete - nothing additional needed
    }

    public void OnFrameEnd()
    {
        // Frame is complete - compositing will happen during Render()
    }

    public void OnSetContext(byte layerId, LayerContext context)
    {
        // Context is managed by the decoder and passed to draw operations
    }

    public void OnSaveContext(byte layerId)
    {
        // Context stack is managed by the decoder
    }

    public void OnRestoreContext(byte layerId)
    {
        // Context stack is managed by the decoder
    }

    public void OnResetContext(byte layerId)
    {
        // Context is managed by the decoder
    }

    public void OnDrawPolygon(byte layerId, ReadOnlySpan<SKPoint> points, LayerContext context)
    {
        if (points.Length == 0) return;

        var layer = _layerManager.GetLayer(layerId);
        var canvas = layer.Canvas;

        // Apply transform
        canvas.Save();
        ApplyTransform(canvas, context);

        // Get paint
        var paint = SKPaintCache.Instance.GetStrokePaint(context.Stroke, (ushort)context.Thickness);

        // Draw polygon using reusable path
        var path = _reusablePath ??= new SKPath();
        path.Reset();

        path.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++)
        {
            path.LineTo(points[i]);
        }
        path.Close();

        canvas.DrawPath(path, paint);
        canvas.Restore();
    }

    public void OnDrawText(byte layerId, string text, int x, int y, LayerContext context)
    {
        var layer = _layerManager.GetLayer(layerId);
        var canvas = layer.Canvas;

        canvas.Save();
        ApplyTransform(canvas, context);

        var (paint, font) = SKPaintCache.Instance.GetTextPaint(context.FontColor, (ushort)context.FontSize);
        canvas.DrawText(text, x, y, font, paint);

        canvas.Restore();
    }

    public void OnDrawCircle(byte layerId, int centerX, int centerY, int radius, LayerContext context)
    {
        var layer = _layerManager.GetLayer(layerId);
        var canvas = layer.Canvas;

        canvas.Save();
        ApplyTransform(canvas, context);

        var paint = SKPaintCache.Instance.GetStrokePaint(context.Stroke, (ushort)context.Thickness);
        canvas.DrawCircle(centerX, centerY, radius, paint);

        canvas.Restore();
    }

    public void OnDrawRect(byte layerId, int x, int y, int width, int height, LayerContext context)
    {
        var layer = _layerManager.GetLayer(layerId);
        var canvas = layer.Canvas;

        canvas.Save();
        ApplyTransform(canvas, context);

        var paint = SKPaintCache.Instance.GetStrokePaint(context.Stroke, (ushort)context.Thickness);
        canvas.DrawRect(x, y, width, height, paint);

        canvas.Restore();
    }

    public void OnDrawLine(byte layerId, int x1, int y1, int x2, int y2, LayerContext context)
    {
        var layer = _layerManager.GetLayer(layerId);
        var canvas = layer.Canvas;

        canvas.Save();
        ApplyTransform(canvas, context);

        var paint = SKPaintCache.Instance.GetStrokePaint(context.Stroke, (ushort)context.Thickness);
        canvas.DrawLine(x1, y1, x2, y2, paint);

        canvas.Restore();
    }

    /// <summary>
    /// Applies the context's transform to the canvas.
    /// </summary>
    private static void ApplyTransform(SKCanvas canvas, LayerContext context)
    {
        var matrix = context.GetTransformMatrix();
        if (!matrix.IsIdentity)
        {
            canvas.Concat(ref matrix);
        }
    }
}
