using System.Buffers.Binary;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Encodes VectorGraphics binary protocol for efficient WebSocket streaming.
/// Uses varint + zigzag encoding for compact data transfer.
/// </summary>
/// <remarks>
/// Wire format matches VectorGraphicsDecoder:
/// Frame:
///   [FrameType: 1 byte]         // 0x00 = master, 0x01 = delta
///   [FrameId: 8 bytes LE]
///   [LayerId: 1 byte]
///   [ObjectCount: varint]
///   For each object:
///       [ObjectType: 1 byte]    // 1=Polygon, 2=Text, 3=Circle, 4=Rect
///       [ObjectData: type-specific binary encoding]
///       [ContextFlags: 2 bytes LE (ushort)]  // Which context fields follow
///       [Context: optional DrawContext fields]
///   [EndMarker: 2 bytes = 0xFF 0xFF]
/// </remarks>
public static class VectorGraphicsEncoder
{
    private const byte FrameTypeMaster = 0x00;
    private const byte FrameTypeDelta = 0x01;

    private const byte ObjectTypePolygon = 1;
    private const byte ObjectTypeText = 2;
    private const byte ObjectTypeCircle = 3;
    private const byte ObjectTypeRect = 4;

    // Context flags (ushort - 16 bits)
    public const ushort ContextHasStroke = 0x0001;
    public const ushort ContextHasFill = 0x0002;
    public const ushort ContextHasThickness = 0x0004;
    public const ushort ContextHasFontSize = 0x0008;
    public const ushort ContextHasFontColor = 0x0010;
    public const ushort ContextHasOffset = 0x0020;
    public const ushort ContextHasRotation = 0x0040;
    public const ushort ContextHasScale = 0x0080;
    public const ushort ContextHasSkew = 0x0100;

    /// <summary>
    /// Begins encoding a new frame. Call this first.
    /// </summary>
    public static int WriteFrameHeader(Span<byte> buffer, ulong frameId, byte layerId, bool isDelta = false)
    {
        int offset = 0;
        buffer[offset++] = isDelta ? FrameTypeDelta : FrameTypeMaster;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset), frameId);
        offset += 8;
        buffer[offset++] = layerId;
        return offset;
    }

    /// <summary>
    /// Writes the object count for the frame.
    /// </summary>
    public static int WriteObjectCount(Span<byte> buffer, uint count)
    {
        return BinaryEncoding.WriteVarint(buffer, count);
    }

    /// <summary>
    /// Encodes a polygon with optional context.
    /// </summary>
    public static int WritePolygon(Span<byte> buffer, ReadOnlySpan<SKPoint> points, DrawContext? context = null)
    {
        int offset = 0;

        // Object type
        buffer[offset++] = ObjectTypePolygon;

        // Point count
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)points.Length);

        if (points.Length > 0)
        {
            // First point (absolute)
            int firstX = (int)points[0].X;
            int firstY = (int)points[0].Y;
            offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), firstX);
            offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), firstY);

            // Remaining points (delta encoded)
            int lastX = firstX;
            int lastY = firstY;
            for (int i = 1; i < points.Length; i++)
            {
                int x = (int)points[i].X;
                int y = (int)points[i].Y;
                offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), x - lastX);
                offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), y - lastY);
                lastX = x;
                lastY = y;
            }
        }

        // Context
        offset += WriteContext(buffer.Slice(offset), context);

        return offset;
    }

    /// <summary>
    /// Encodes text with optional context.
    /// </summary>
    public static int WriteText(Span<byte> buffer, string text, int x, int y, DrawContext? context = null)
    {
        int offset = 0;

        // Object type
        buffer[offset++] = ObjectTypeText;

        // Position
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), x);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), y);

        // Text length and content
        var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)textBytes.Length);
        textBytes.CopyTo(buffer.Slice(offset));
        offset += textBytes.Length;

        // Context
        offset += WriteContext(buffer.Slice(offset), context);

        return offset;
    }

    /// <summary>
    /// Encodes a rectangle with optional context.
    /// </summary>
    public static int WriteRectangle(Span<byte> buffer, int x, int y, int width, int height, DrawContext? context = null)
    {
        int offset = 0;

        // Object type
        buffer[offset++] = ObjectTypeRect;

        // Position and size
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), x);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), y);
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)width);
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)height);

        // Context
        offset += WriteContext(buffer.Slice(offset), context);

        return offset;
    }

    /// <summary>
    /// Encodes a circle with optional context.
    /// </summary>
    public static int WriteCircle(Span<byte> buffer, int centerX, int centerY, int radius, DrawContext? context = null)
    {
        int offset = 0;

        // Object type
        buffer[offset++] = ObjectTypeCircle;

        // Center and radius
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), centerX);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), centerY);
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)radius);

        // Context
        offset += WriteContext(buffer.Slice(offset), context);

        return offset;
    }

    /// <summary>
    /// Writes the end marker for a frame.
    /// </summary>
    public static int WriteEndMarker(Span<byte> buffer)
    {
        buffer[0] = 0xFF;
        buffer[1] = 0xFF;
        return 2;
    }

    /// <summary>
    /// Encodes a DrawContext with ushort flags.
    /// </summary>
    public static int WriteContext(Span<byte> buffer, DrawContext? context)
    {
        if (context == null)
        {
            // No context - write zero flags
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, 0);
            return 2;
        }

        var ctx = context.Value;
        ushort flags = 0;
        int offset = 2; // Reserve space for flags

        // Stroke color
        if (ctx.Stroke.HasValue)
        {
            flags |= ContextHasStroke;
            var color = ctx.Stroke.Value;
            buffer[offset++] = color.R;
            buffer[offset++] = color.G;
            buffer[offset++] = color.B;
            buffer[offset++] = color.A;
        }

        // Fill color
        if (ctx.Fill.HasValue)
        {
            flags |= ContextHasFill;
            var color = ctx.Fill.Value;
            buffer[offset++] = color.R;
            buffer[offset++] = color.G;
            buffer[offset++] = color.B;
            buffer[offset++] = color.A;
        }

        // Thickness
        if (ctx.Thickness > 0)
        {
            flags |= ContextHasThickness;
            offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), ctx.Thickness);
        }

        // Font size
        if (ctx.FontSize > 0)
        {
            flags |= ContextHasFontSize;
            offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), ctx.FontSize);
        }

        // Font color
        if (ctx.FontColor.HasValue)
        {
            flags |= ContextHasFontColor;
            var color = ctx.FontColor.Value;
            buffer[offset++] = color.R;
            buffer[offset++] = color.G;
            buffer[offset++] = color.B;
            buffer[offset++] = color.A;
        }

        // Offset (translate)
        if (ctx.Offset.HasValue)
        {
            flags |= ContextHasOffset;
            offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), (int)ctx.Offset.Value.X);
            offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), (int)ctx.Offset.Value.Y);
        }

        // Rotation (degrees, float)
        if (ctx.Rotation.HasValue)
        {
            flags |= ContextHasRotation;
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset), ctx.Rotation.Value);
            offset += 4;
        }

        // Scale (X, Y floats)
        if (ctx.Scale.HasValue)
        {
            flags |= ContextHasScale;
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset), ctx.Scale.Value.X);
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset + 4), ctx.Scale.Value.Y);
            offset += 8;
        }

        // Skew (X, Y floats)
        if (ctx.Skew.HasValue)
        {
            flags |= ContextHasSkew;
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset), ctx.Skew.Value.X);
            BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset + 4), ctx.Skew.Value.Y);
            offset += 8;
        }

        // Write flags at the beginning
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, flags);

        return offset;
    }

    /// <summary>
    /// Encodes a complete frame with polygons.
    /// </summary>
    public static int EncodePolygonFrame(
        Span<byte> buffer,
        ulong frameId,
        byte layerId,
        ReadOnlySpan<SKPoint[]> polygons,
        ReadOnlySpan<DrawContext> contexts)
    {
        int offset = WriteFrameHeader(buffer, frameId, layerId);
        offset += WriteObjectCount(buffer.Slice(offset), (uint)polygons.Length);

        for (int i = 0; i < polygons.Length; i++)
        {
            var context = i < contexts.Length ? contexts[i] : (DrawContext?)null;
            offset += WritePolygon(buffer.Slice(offset), polygons[i], context);
        }

        offset += WriteEndMarker(buffer.Slice(offset));
        return offset;
    }
}
