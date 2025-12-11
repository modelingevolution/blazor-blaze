using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Decodes VectorGraphics binary protocol into canvas rendering commands.
/// Uses varint + zigzag encoding for efficient data transfer.
/// </summary>
/// <remarks>
/// Wire format:
/// Frame:
///   [FrameType: 1 byte]         // 0x00 = master, 0x01 = delta
///   [FrameId: 8 bytes LE]
///   [LayerId: 1 byte]
///   [ObjectCount: varint]
///   For each object:
///       [ObjectType: 1 byte]    // 1=Polygon, 2=Text, 3=Circle, 4=Rect
///       [ObjectData: type-specific binary encoding]
///       [HasContext: 1 byte]    // 0 or flags
///       [Context: optional DrawContext]
///   [EndMarker: 2 bytes = 0xFF 0xFF]
/// </remarks>
public class VectorGraphicsDecoder : IFrameDecoder
{
    private const byte FrameTypeMaster = 0x00;
    private const byte FrameTypeDelta = 0x01;

    private const byte ObjectTypePolygon = 1;
    private const byte ObjectTypeText = 2;
    private const byte ObjectTypeCircle = 3;
    private const byte ObjectTypeRect = 4;

    private const byte EndMarker1 = 0xFF;
    private const byte EndMarker2 = 0xFF;

    // Context flags (ushort - 16 bits)
    private const ushort ContextHasStroke = 0x0001;
    private const ushort ContextHasFill = 0x0002;
    private const ushort ContextHasThickness = 0x0004;
    private const ushort ContextHasFontSize = 0x0008;
    private const ushort ContextHasFontColor = 0x0010;
    private const ushort ContextHasOffset = 0x0020;
    private const ushort ContextHasRotation = 0x0040;
    private const ushort ContextHasScale = 0x0080;
    private const ushort ContextHasSkew = 0x0100;

    private readonly VectorGraphicsOptions _options;
    private readonly HashSet<int>? _filteredLayers;

    public VectorGraphicsDecoder(VectorGraphicsOptions options)
    {
        _options = options;
        _filteredLayers = options.FilteredLayers?.ToHashSet();
    }

    public DecodeResult Decode(in ReadOnlySpan<byte> buffer, ICanvas canvas)
    {
        if (buffer.Length < 11) // Minimum: FrameType(1) + FrameId(8) + LayerId(1) + ObjectCount(1)
            return DecodeResult.NeedMoreData();

        int offset = 0;

        // Read frame type
        byte frameType = buffer[offset++];
        if (frameType != FrameTypeMaster && frameType != FrameTypeDelta)
            throw new InvalidOperationException($"Unknown frame type: {frameType}");

        // Read frame ID (8 bytes LE)
        ulong frameId = BitConverter.ToUInt64(buffer.Slice(offset, 8));
        offset += 8;

        // Read layer ID
        byte layerId = buffer[offset++];

        // Check if this layer should be filtered
        bool shouldRender = _filteredLayers == null || _filteredLayers.Contains(layerId);

        // Read object count
        int consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint objectCount);
        if (consumed == 0)
            return DecodeResult.NeedMoreData();
        offset += consumed;

        // Begin drawing on the layer
        if (shouldRender)
            canvas.Begin(frameId, layerId);

        // Decode each object
        for (uint i = 0; i < objectCount; i++)
        {
            if (offset >= buffer.Length)
                return DecodeResult.NeedMoreData();

            // Check for end marker
            if (buffer.Length - offset >= 2 && buffer[offset] == EndMarker1 && buffer[offset + 1] == EndMarker2)
            {
                offset += 2;
                break;
            }

            int objectBytes = DecodeObject(buffer.Slice(offset), canvas, shouldRender, layerId);
            if (objectBytes == 0)
                return DecodeResult.NeedMoreData();
            offset += objectBytes;
        }

        // Look for end marker if we haven't found it yet
        if (buffer.Length - offset >= 2 && buffer[offset] == EndMarker1 && buffer[offset + 1] == EndMarker2)
        {
            offset += 2;
        }

        // End drawing on the layer
        if (shouldRender)
            canvas.End(layerId);

        return DecodeResult.Frame(frameId, offset);
    }

    private int DecodeObject(ReadOnlySpan<byte> buffer, ICanvas canvas, bool shouldRender, byte layerId)
    {
        if (buffer.Length < 1)
            return 0;

        int offset = 0;
        byte objectType = buffer[offset++];

        int dataBytes;
        switch (objectType)
        {
            case ObjectTypePolygon:
                dataBytes = DecodePolygon(buffer.Slice(offset), canvas, shouldRender, layerId, out var context1);
                break;
            case ObjectTypeText:
                dataBytes = DecodeText(buffer.Slice(offset), canvas, shouldRender, layerId, out var context2);
                break;
            case ObjectTypeCircle:
                dataBytes = DecodeCircle(buffer.Slice(offset), canvas, shouldRender, layerId, out var context3);
                break;
            case ObjectTypeRect:
                dataBytes = DecodeRect(buffer.Slice(offset), canvas, shouldRender, layerId, out var context4);
                break;
            default:
                throw new InvalidOperationException($"Unknown object type: {objectType}");
        }

        if (dataBytes == 0)
            return 0;

        return 1 + dataBytes;
    }

    private int DecodePolygon(ReadOnlySpan<byte> buffer, ICanvas canvas, bool shouldRender, byte layerId, out DrawContext? context)
    {
        context = null;
        int offset = 0;

        // Read point count
        int consumed = BinaryEncoding.ReadVarint(buffer, out uint pointCount);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Rent from ArrayPool instead of allocating new array
        var points = ArrayPool<SKPoint>.Shared.Rent((int)pointCount);
        try
        {
            int lastX = 0, lastY = 0;

            for (int i = 0; i < pointCount; i++)
            {
                // Read X (zigzag + varint for delta)
                consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int deltaX);
                if (consumed == 0)
                {
                    ArrayPool<SKPoint>.Shared.Return(points);
                    return 0;
                }
                offset += consumed;

                // Read Y (zigzag + varint for delta)
                consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int deltaY);
                if (consumed == 0)
                {
                    ArrayPool<SKPoint>.Shared.Return(points);
                    return 0;
                }
                offset += consumed;

                if (i == 0)
                {
                    // First point is absolute
                    lastX = deltaX;
                    lastY = deltaY;
                }
                else
                {
                    // Subsequent points are deltas
                    lastX += deltaX;
                    lastY += deltaY;
                }

                // Direct decode to SKPoint - no conversion overhead at render time
                points[i] = new SKPoint(Math.Max(0, lastX), Math.Max(0, lastY));
            }

            // Read context
            consumed = DecodeContext(buffer.Slice(offset), out context);
            if (consumed < 0)
            {
                ArrayPool<SKPoint>.Shared.Return(points);
                return 0;
            }
            offset += consumed;

            if (shouldRender)
            {
                // Pass as span with actual point count - canvas will handle pooling for buffered ops
                canvas.DrawPolygon(points.AsSpan(0, (int)pointCount), context?.Stroke, context?.Thickness ?? 1, layerId);
            }
        }
        finally
        {
            // Return array to pool - we've already copied to canvas buffer if rendering
            ArrayPool<SKPoint>.Shared.Return(points);
        }

        return offset;
    }

    private int DecodeText(ReadOnlySpan<byte> buffer, ICanvas canvas, bool shouldRender, byte layerId, out DrawContext? context)
    {
        context = null;
        int offset = 0;

        // Read X
        int consumed = BinaryEncoding.ReadSignedVarint(buffer, out int x);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read Y
        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int y);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read text length
        consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint textLength);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read text bytes
        if (buffer.Length - offset < textLength)
            return 0;
        string text = Encoding.UTF8.GetString(buffer.Slice(offset, (int)textLength));
        offset += (int)textLength;

        // Read context
        consumed = DecodeContext(buffer.Slice(offset), out context);
        if (consumed < 0)
            return 0;
        offset += consumed;

        if (shouldRender)
        {
            canvas.DrawText(text, Math.Max(0, x), Math.Max(0, y),
                context?.FontSize ?? 12, context?.FontColor, layerId);
        }

        return offset;
    }

    private int DecodeCircle(ReadOnlySpan<byte> buffer, ICanvas canvas, bool shouldRender, byte layerId, out DrawContext? context)
    {
        context = null;
        int offset = 0;

        // Read center X
        int consumed = BinaryEncoding.ReadSignedVarint(buffer, out int cx);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read center Y
        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int cy);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read radius
        consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint radius);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read context
        consumed = DecodeContext(buffer.Slice(offset), out context);
        if (consumed < 0)
            return 0;
        offset += consumed;

        // Circle rendering would need to be added to ICanvas
        // For now, approximate with rectangle
        if (shouldRender)
        {
            var rect = new System.Drawing.Rectangle(
                (int)Math.Max(0, cx - radius),
                (int)Math.Max(0, cy - radius),
                (int)(radius * 2),
                (int)(radius * 2));
            canvas.DrawRectangle(rect, context?.Stroke, layerId);
        }

        return offset;
    }

    private int DecodeRect(ReadOnlySpan<byte> buffer, ICanvas canvas, bool shouldRender, byte layerId, out DrawContext? context)
    {
        context = null;
        int offset = 0;

        // Read X
        int consumed = BinaryEncoding.ReadSignedVarint(buffer, out int x);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read Y
        consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int y);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read width
        consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint width);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read height
        consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint height);
        if (consumed == 0)
            return 0;
        offset += consumed;

        // Read context
        consumed = DecodeContext(buffer.Slice(offset), out context);
        if (consumed < 0)
            return 0;
        offset += consumed;

        if (shouldRender)
        {
            var rect = new System.Drawing.Rectangle(
                Math.Max(0, x),
                Math.Max(0, y),
                (int)width,
                (int)height);
            canvas.DrawRectangle(rect, context?.Stroke, layerId);
        }

        return offset;
    }

    private int DecodeContext(ReadOnlySpan<byte> buffer, out DrawContext? context)
    {
        context = null;

        if (buffer.Length < 2)
            return -1;

        // Read ushort flags (2 bytes LE)
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        if (flags == 0)
            return 2; // No context, just the flags bytes

        int offset = 2;

        RgbColor? stroke = null;
        RgbColor? fill = null;
        ushort thickness = 1;
        ushort fontSize = 12;
        RgbColor? fontColor = null;
        SKPoint? contextOffset = null;
        float? rotation = null;
        SKPoint? scale = null;
        SKPoint? skew = null;

        // Read stroke color (4 bytes RGBA)
        if ((flags & ContextHasStroke) != 0)
        {
            if (buffer.Length - offset < 4)
                return -1;
            stroke = new RgbColor(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);
            offset += 4;
        }

        // Read fill color (4 bytes RGBA)
        if ((flags & ContextHasFill) != 0)
        {
            if (buffer.Length - offset < 4)
                return -1;
            fill = new RgbColor(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);
            offset += 4;
        }

        // Read thickness (varint)
        if ((flags & ContextHasThickness) != 0)
        {
            int consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint thicknessVal);
            if (consumed == 0)
                return -1;
            thickness = (ushort)thicknessVal;
            offset += consumed;
        }

        // Read font size (varint)
        if ((flags & ContextHasFontSize) != 0)
        {
            int consumed = BinaryEncoding.ReadVarint(buffer.Slice(offset), out uint fontSizeVal);
            if (consumed == 0)
                return -1;
            fontSize = (ushort)fontSizeVal;
            offset += consumed;
        }

        // Read font color (4 bytes RGBA)
        if ((flags & ContextHasFontColor) != 0)
        {
            if (buffer.Length - offset < 4)
                return -1;
            fontColor = new RgbColor(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);
            offset += 4;
        }

        // Read offset/translate (2 x zigzag varint) - decode directly to SKPoint
        if ((flags & ContextHasOffset) != 0)
        {
            int consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int ox);
            if (consumed == 0)
                return -1;
            offset += consumed;

            consumed = BinaryEncoding.ReadSignedVarint(buffer.Slice(offset), out int oy);
            if (consumed == 0)
                return -1;
            offset += consumed;

            contextOffset = new SKPoint(ox, oy);
        }

        // Read rotation (4 bytes float LE, degrees)
        if ((flags & ContextHasRotation) != 0)
        {
            if (buffer.Length - offset < 4)
                return -1;
            rotation = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset));
            offset += 4;
        }

        // Read scale (2 x 4 bytes float LE)
        if ((flags & ContextHasScale) != 0)
        {
            if (buffer.Length - offset < 8)
                return -1;
            float scaleX = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset));
            float scaleY = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4));
            scale = new SKPoint(scaleX, scaleY);
            offset += 8;
        }

        // Read skew (2 x 4 bytes float LE)
        if ((flags & ContextHasSkew) != 0)
        {
            if (buffer.Length - offset < 8)
                return -1;
            float skewX = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset));
            float skewY = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4));
            skew = new SKPoint(skewX, skewY);
            offset += 8;
        }

        context = new DrawContext
        {
            Stroke = stroke,
            Fill = fill,
            Thickness = thickness,
            FontSize = fontSize,
            FontColor = fontColor,
            Offset = contextOffset,
            Rotation = rotation,
            Scale = scale,
            Skew = skew
        };

        return offset;
    }

    public void Reset()
    {
        // No state to reset in this implementation
    }
}
