using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Result of a decode operation.
/// </summary>
public readonly struct DecodeResultV2
{
    public bool Success { get; init; }
    public int BytesConsumed { get; init; }
    public ulong? FrameId { get; init; }
    public byte LayerCount { get; init; }

    public static DecodeResultV2 NeedMoreData => new() { Success = false, BytesConsumed = 0 };

    public static DecodeResultV2 Ok(int consumed, ulong frameId, byte layerCount) => new()
    {
        Success = true,
        BytesConsumed = consumed,
        FrameId = frameId,
        LayerCount = layerCount
    };
}

/// <summary>
/// Per-layer context state maintained during decoding.
/// </summary>
public class LayerContext
{
    public RgbColor Stroke { get; set; } = RgbColor.Black;
    public RgbColor Fill { get; set; } = RgbColor.Transparent;
    public int Thickness { get; set; } = 1;
    public int FontSize { get; set; } = 12;
    public RgbColor FontColor { get; set; } = RgbColor.Black;
    public SKPoint Offset { get; set; } = SKPoint.Empty;
    public float Rotation { get; set; } = 0;
    public SKPoint Scale { get; set; } = new(1, 1);
    public SKPoint Skew { get; set; } = SKPoint.Empty;
    public SKMatrix? Matrix { get; set; }

    private readonly Stack<LayerContext> _savedStates = new();

    public void Save()
    {
        _savedStates.Push(Clone());
    }

    public void Restore()
    {
        if (_savedStates.Count > 0)
        {
            var saved = _savedStates.Pop();
            CopyFrom(saved);
        }
    }

    public void Reset()
    {
        Stroke = RgbColor.Black;
        Fill = RgbColor.Transparent;
        Thickness = 1;
        FontSize = 12;
        FontColor = RgbColor.Black;
        Offset = SKPoint.Empty;
        Rotation = 0;
        Scale = new SKPoint(1, 1);
        Skew = SKPoint.Empty;
        Matrix = null;
        _savedStates.Clear();
    }

    private LayerContext Clone() => new()
    {
        Stroke = Stroke,
        Fill = Fill,
        Thickness = Thickness,
        FontSize = FontSize,
        FontColor = FontColor,
        Offset = Offset,
        Rotation = Rotation,
        Scale = Scale,
        Skew = Skew,
        Matrix = Matrix
    };

    private void CopyFrom(LayerContext other)
    {
        Stroke = other.Stroke;
        Fill = other.Fill;
        Thickness = other.Thickness;
        FontSize = other.FontSize;
        FontColor = other.FontColor;
        Offset = other.Offset;
        Rotation = other.Rotation;
        Scale = other.Scale;
        Skew = other.Skew;
        Matrix = other.Matrix;
    }

    /// <summary>
    /// Builds the combined transformation matrix from individual transform properties.
    /// </summary>
    public SKMatrix GetTransformMatrix()
    {
        if (Matrix.HasValue)
            return Matrix.Value;

        var result = SKMatrix.Identity;

        // Apply transforms in order: translate, rotate, scale, skew
        if (Offset != SKPoint.Empty)
            result = result.PostConcat(SKMatrix.CreateTranslation(Offset.X, Offset.Y));

        if (Rotation != 0)
            result = result.PostConcat(SKMatrix.CreateRotationDegrees(Rotation));

        if (Scale.X != 1 || Scale.Y != 1)
            result = result.PostConcat(SKMatrix.CreateScale(Scale.X, Scale.Y));

        if (Skew.X != 0 || Skew.Y != 0)
            result = result.PostConcat(SKMatrix.CreateSkew(Skew.X, Skew.Y));

        return result;
    }
}

/// <summary>
/// Callback interface for decoder to notify about decoded operations.
/// </summary>
public interface IDecoderCallbackV2
{
    void OnFrameStart(ulong frameId, byte layerCount);
    void OnLayerStart(byte layerId, FrameType frameType);
    void OnLayerEnd(byte layerId);
    void OnFrameEnd();

    void OnSetContext(byte layerId, LayerContext context);
    void OnSaveContext(byte layerId);
    void OnRestoreContext(byte layerId);
    void OnResetContext(byte layerId);

    void OnDrawPolygon(byte layerId, ReadOnlySpan<SKPoint> points, LayerContext context);
    void OnDrawText(byte layerId, string text, int x, int y, LayerContext context);
    void OnDrawCircle(byte layerId, int centerX, int centerY, int radius, LayerContext context);
    void OnDrawRect(byte layerId, int x, int y, int width, int height, LayerContext context);
    void OnDrawLine(byte layerId, int x1, int y1, int x2, int y2, LayerContext context);
}

/// <summary>
/// Protocol v2 decoder for stateful canvas API with multi-layer support.
/// </summary>
public class VectorGraphicsDecoderV2
{
    private const int MinMessageSize = 9 + 2; // Header (9) + EndMarker (2)

    private readonly Dictionary<byte, LayerContext> _layerContexts = new();
    private SKPoint[] _pointBuffer = new SKPoint[256];

    /// <summary>
    /// Gets or creates the context for a layer.
    /// </summary>
    private LayerContext GetLayerContext(byte layerId)
    {
        if (!_layerContexts.TryGetValue(layerId, out var context))
        {
            context = new LayerContext();
            _layerContexts[layerId] = context;
        }
        return context;
    }

    /// <summary>
    /// Resets all layer contexts.
    /// </summary>
    public void Reset()
    {
        foreach (var ctx in _layerContexts.Values)
            ctx.Reset();
    }

    /// <summary>
    /// Decodes a complete message from the buffer.
    /// </summary>
    public DecodeResultV2 Decode(ReadOnlySpan<byte> buffer, IDecoderCallbackV2 callback)
    {
        if (buffer.Length < MinMessageSize)
            return DecodeResultV2.NeedMoreData;

        int offset = 0;

        // Read message header
        ulong frameId = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        offset += 8;
        byte layerCount = buffer[offset++];

        callback.OnFrameStart(frameId, layerCount);

        // Read layer blocks
        for (int i = 0; i < layerCount; i++)
        {
            if (offset + 2 > buffer.Length)
                return DecodeResultV2.NeedMoreData;

            byte layerId = buffer[offset++];
            var frameType = (FrameType)buffer[offset++];

            callback.OnLayerStart(layerId, frameType);

            if (frameType == FrameType.Master)
            {
                // Read operation count
                int consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint opCount);
                if (consumed == 0)
                    return DecodeResultV2.NeedMoreData;
                offset += consumed;

                var context = GetLayerContext(layerId);

                // Read operations
                for (uint op = 0; op < opCount; op++)
                {
                    if (offset >= buffer.Length)
                        return DecodeResultV2.NeedMoreData;

                    consumed = DecodeOperation(buffer.Slice(offset), layerId, context, callback);
                    if (consumed == 0)
                        return DecodeResultV2.NeedMoreData;
                    offset += consumed;
                }
            }
            // Remain and Clear have no additional data

            callback.OnLayerEnd(layerId);
        }

        // Verify end marker
        if (offset + 2 > buffer.Length)
            return DecodeResultV2.NeedMoreData;

        if (buffer[offset] != ProtocolV2.EndMarkerByte1 || buffer[offset + 1] != ProtocolV2.EndMarkerByte2)
            throw new InvalidOperationException("Invalid end marker");

        offset += 2;

        callback.OnFrameEnd();

        return DecodeResultV2.Ok(offset, frameId, layerCount);
    }

    /// <summary>
    /// Decodes a single operation.
    /// </summary>
    private int DecodeOperation(ReadOnlySpan<byte> buffer, byte layerId, LayerContext context, IDecoderCallbackV2 callback)
    {
        if (buffer.IsEmpty)
            return 0;

        var opType = (OpType)buffer[0];
        int offset = 1;

        switch (opType)
        {
            case OpType.SetContext:
                offset += DecodeSetContext(buffer.Slice(offset), layerId, context, callback);
                break;

            case OpType.SaveContext:
                context.Save();
                callback.OnSaveContext(layerId);
                break;

            case OpType.RestoreContext:
                context.Restore();
                callback.OnRestoreContext(layerId);
                break;

            case OpType.ResetContext:
                context.Reset();
                callback.OnResetContext(layerId);
                break;

            case OpType.DrawPolygon:
                offset += DecodeDrawPolygon(buffer.Slice(offset), layerId, context, callback);
                break;

            case OpType.DrawText:
                offset += DecodeDrawText(buffer.Slice(offset), layerId, context, callback);
                break;

            case OpType.DrawCircle:
                offset += DecodeDrawCircle(buffer.Slice(offset), layerId, context, callback);
                break;

            case OpType.DrawRect:
                offset += DecodeDrawRect(buffer.Slice(offset), layerId, context, callback);
                break;

            case OpType.DrawLine:
                offset += DecodeDrawLine(buffer.Slice(offset), layerId, context, callback);
                break;

            default:
                throw new InvalidOperationException($"Unknown operation type: {opType}");
        }

        return offset;
    }

    private int DecodeSetContext(ReadOnlySpan<byte> buffer, byte layerId, LayerContext context, IDecoderCallbackV2 callback)
    {
        int offset = 0;

        int consumed = BinaryEncoding.ReadVarint(buffer, out uint propertyCount);
        if (consumed == 0) return 0;
        offset += consumed;

        for (uint i = 0; i < propertyCount; i++)
        {
            consumed = DecodeProperty(buffer.Slice(offset), context);
            if (consumed == 0) return 0;
            offset += consumed;
        }

        callback.OnSetContext(layerId, context);
        return offset;
    }

    private int DecodeProperty(ReadOnlySpan<byte> buffer, LayerContext context)
    {
        if (buffer.IsEmpty) return 0;

        var propertyId = (PropertyId)buffer[0];
        int offset = 1;

        switch (propertyId)
        {
            case PropertyId.Stroke:
                if (offset + 4 > buffer.Length) return 0;
                context.Stroke = new RgbColor(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);
                offset += 4;
                break;

            case PropertyId.Fill:
                if (offset + 4 > buffer.Length) return 0;
                context.Fill = new RgbColor(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);
                offset += 4;
                break;

            case PropertyId.FontColor:
                if (offset + 4 > buffer.Length) return 0;
                context.FontColor = new RgbColor(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);
                offset += 4;
                break;

            case PropertyId.Thickness:
                int consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint thickness);
                if (consumed == 0) return 0;
                context.Thickness = (int)thickness;
                offset += consumed;
                break;

            case PropertyId.FontSize:
                consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint fontSize);
                if (consumed == 0) return 0;
                context.FontSize = (int)fontSize;
                offset += consumed;
                break;

            case PropertyId.Offset:
                consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int offsetX);
                if (consumed == 0) return 0;
                offset += consumed;
                consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int offsetY);
                if (consumed == 0) return 0;
                offset += consumed;
                context.Offset = new SKPoint(offsetX, offsetY);
                break;

            case PropertyId.Rotation:
                if (offset + 4 > buffer.Length) return 0;
                context.Rotation = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset));
                offset += 4;
                break;

            case PropertyId.Scale:
                if (offset + 8 > buffer.Length) return 0;
                float scaleX = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset));
                float scaleY = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4));
                context.Scale = new SKPoint(scaleX, scaleY);
                offset += 8;
                break;

            case PropertyId.Skew:
                if (offset + 8 > buffer.Length) return 0;
                float skewX = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset));
                float skewY = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4));
                context.Skew = new SKPoint(skewX, skewY);
                offset += 8;
                break;

            case PropertyId.Matrix:
                if (offset + 24 > buffer.Length) return 0;
                context.Matrix = new SKMatrix(
                    BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset)),      // ScaleX
                    BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4)),  // SkewX
                    BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 8)),  // TransX
                    BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 12)), // SkewY
                    BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 16)), // ScaleY
                    BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 20)), // TransY
                    0, 0, 1);
                offset += 24;
                break;

            default:
                throw new InvalidOperationException($"Unknown property ID: {propertyId}");
        }

        return offset;
    }

    private int DecodeDrawPolygon(ReadOnlySpan<byte> buffer, byte layerId, LayerContext context, IDecoderCallbackV2 callback)
    {
        int offset = 0;

        int consumed = BinaryEncoding.ReadVarint(buffer, out uint pointCount);
        if (consumed == 0) return 0;
        offset += consumed;

        // Ensure buffer is large enough
        if (_pointBuffer.Length < pointCount)
            _pointBuffer = new SKPoint[(int)pointCount * 2];

        if (pointCount > 0)
        {
            // First point (absolute)
            consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int x);
            if (consumed == 0) return 0;
            offset += consumed;

            consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int y);
            if (consumed == 0) return 0;
            offset += consumed;

            _pointBuffer[0] = new SKPoint(x, y);

            // Remaining points (delta encoded)
            int lastX = x;
            int lastY = y;
            for (int i = 1; i < pointCount; i++)
            {
                consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int dx);
                if (consumed == 0) return 0;
                offset += consumed;

                consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int dy);
                if (consumed == 0) return 0;
                offset += consumed;

                lastX += dx;
                lastY += dy;
                _pointBuffer[i] = new SKPoint(lastX, lastY);
            }
        }

        callback.OnDrawPolygon(layerId, _pointBuffer.AsSpan(0, (int)pointCount), context);
        return offset;
    }

    private int DecodeDrawText(ReadOnlySpan<byte> buffer, byte layerId, LayerContext context, IDecoderCallbackV2 callback)
    {
        int offset = 0;

        int consumed = BinaryEncoding.ReadSignedVarint(buffer, out int x);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int y);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint textLength);
        if (consumed == 0) return 0;
        offset += consumed;

        if (offset + textLength > buffer.Length) return 0;

        string text = System.Text.Encoding.UTF8.GetString(buffer.Slice(offset, (int)textLength));
        offset += (int)textLength;

        callback.OnDrawText(layerId, text, x, y, context);
        return offset;
    }

    private int DecodeDrawCircle(ReadOnlySpan<byte> buffer, byte layerId, LayerContext context, IDecoderCallbackV2 callback)
    {
        int offset = 0;

        int consumed = BinaryEncoding.ReadSignedVarint(buffer, out int centerX);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int centerY);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint radius);
        if (consumed == 0) return 0;
        offset += consumed;

        callback.OnDrawCircle(layerId, centerX, centerY, (int)radius, context);
        return offset;
    }

    private int DecodeDrawRect(ReadOnlySpan<byte> buffer, byte layerId, LayerContext context, IDecoderCallbackV2 callback)
    {
        int offset = 0;

        int consumed = BinaryEncoding.ReadSignedVarint(buffer, out int x);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int y);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint width);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint height);
        if (consumed == 0) return 0;
        offset += consumed;

        callback.OnDrawRect(layerId, x, y, (int)width, (int)height, context);
        return offset;
    }

    private int DecodeDrawLine(ReadOnlySpan<byte> buffer, byte layerId, LayerContext context, IDecoderCallbackV2 callback)
    {
        int offset = 0;

        int consumed = BinaryEncoding.ReadSignedVarint(buffer, out int x1);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int y1);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int x2);
        if (consumed == 0) return 0;
        offset += consumed;

        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int y2);
        if (consumed == 0) return 0;
        offset += consumed;

        callback.OnDrawLine(layerId, x1, y1, x2, y2, context);
        return offset;
    }
}
