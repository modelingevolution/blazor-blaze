using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Protocol v2 encoder for stateful canvas API with multi-layer support.
/// </summary>
/// <remarks>
/// Wire format:
/// Message:
///   [GlobalFrameId: 8 bytes LE]
///   [LayerCount: 1 byte]
///   For each layer:
///     [LayerBlock...]
///   [EndMarker: 0xFF 0xFF]
///
/// LayerBlock:
///   [LayerId: 1 byte]
///   [FrameType: 1 byte] // 0x00=Master, 0x01=Remain, 0x02=Clear
///   If FrameType == Master:
///     [OpCount: varint]
///     [Operations...]
/// </remarks>
public static class VectorGraphicsEncoderV2
{
    #region Message Header

    /// <summary>
    /// Writes message header with frame ID and layer count.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMessageHeader(Span<byte> buffer, ulong frameId, byte layerCount)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, frameId);
        buffer[8] = layerCount;
        return 9;
    }

    /// <summary>
    /// Writes end marker (0xFF 0xFF).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteEndMarker(Span<byte> buffer)
    {
        buffer[0] = ProtocolV2.EndMarkerByte1;
        buffer[1] = ProtocolV2.EndMarkerByte2;
        return 2;
    }

    #endregion

    #region Layer Block

    /// <summary>
    /// Writes layer block header for Master frame type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLayerMaster(Span<byte> buffer, byte layerId, uint opCount)
    {
        buffer[0] = layerId;
        buffer[1] = (byte)FrameType.Master;
        int offset = 2;
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), opCount);
        return offset;
    }

    /// <summary>
    /// Writes layer block for Remain frame type (keep previous content).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLayerRemain(Span<byte> buffer, byte layerId)
    {
        buffer[0] = layerId;
        buffer[1] = (byte)FrameType.Remain;
        return 2;
    }

    /// <summary>
    /// Writes layer block for Clear frame type (clear to transparent).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLayerClear(Span<byte> buffer, byte layerId)
    {
        buffer[0] = layerId;
        buffer[1] = (byte)FrameType.Clear;
        return 2;
    }

    #endregion

    #region Context Operations

    /// <summary>
    /// Writes SetContext operation with multiple properties.
    /// </summary>
    public static int WriteSetContext(Span<byte> buffer, ReadOnlySpan<(PropertyId id, object value)> properties)
    {
        if (properties.IsEmpty)
            return 0;

        int offset = 0;
        buffer[offset++] = (byte)OpType.SetContext;
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)properties.Length);

        foreach (var (id, value) in properties)
        {
            offset += WriteProperty(buffer.Slice(offset), id, value);
        }

        return offset;
    }

    /// <summary>
    /// Writes SetContext for stroke color.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetStroke(Span<byte> buffer, RgbColor color)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1; // 1 field (varint fits in 1 byte)
        buffer[2] = (byte)PropertyId.Stroke;
        buffer[3] = color.R;
        buffer[4] = color.G;
        buffer[5] = color.B;
        buffer[6] = color.A;
        return 7;
    }

    /// <summary>
    /// Writes SetContext for fill color.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetFill(Span<byte> buffer, RgbColor color)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.Fill;
        buffer[3] = color.R;
        buffer[4] = color.G;
        buffer[5] = color.B;
        buffer[6] = color.A;
        return 7;
    }

    /// <summary>
    /// Writes SetContext for stroke thickness.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetThickness(Span<byte> buffer, int thickness)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.Thickness;
        return 3 + BinaryEncoding.WriteVarint(buffer.Slice(3), (uint)thickness);
    }

    /// <summary>
    /// Writes SetContext for font size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetFontSize(Span<byte> buffer, int size)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.FontSize;
        return 3 + BinaryEncoding.WriteVarint(buffer.Slice(3), (uint)size);
    }

    /// <summary>
    /// Writes SetContext for font color.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetFontColor(Span<byte> buffer, RgbColor color)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.FontColor;
        buffer[3] = color.R;
        buffer[4] = color.G;
        buffer[5] = color.B;
        buffer[6] = color.A;
        return 7;
    }

    /// <summary>
    /// Writes SetContext for translation offset.
    /// </summary>
    public static int WriteSetOffset(Span<byte> buffer, float x, float y)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.Offset;
        int offset = 3;
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), (int)x);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), (int)y);
        return offset;
    }

    /// <summary>
    /// Writes SetContext for rotation in degrees.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetRotation(Span<byte> buffer, float degrees)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.Rotation;
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(3), degrees);
        return 7;
    }

    /// <summary>
    /// Writes SetContext for scale.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetScale(Span<byte> buffer, float scaleX, float scaleY)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.Scale;
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(3), scaleX);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(7), scaleY);
        return 11;
    }

    /// <summary>
    /// Writes SetContext for skew.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetSkew(Span<byte> buffer, float skewX, float skewY)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.Skew;
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(3), skewX);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(7), skewY);
        return 11;
    }

    /// <summary>
    /// Writes SetContext for full transformation matrix (6 floats).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSetMatrix(Span<byte> buffer, SKMatrix matrix)
    {
        buffer[0] = (byte)OpType.SetContext;
        buffer[1] = 1;
        buffer[2] = (byte)PropertyId.Matrix;
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(3), matrix.ScaleX);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(7), matrix.SkewX);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(11), matrix.TransX);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(15), matrix.SkewY);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(19), matrix.ScaleY);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(23), matrix.TransY);
        return 27;
    }

    /// <summary>
    /// Writes SaveContext operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSaveContext(Span<byte> buffer)
    {
        buffer[0] = (byte)OpType.SaveContext;
        return 1;
    }

    /// <summary>
    /// Writes RestoreContext operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteRestoreContext(Span<byte> buffer)
    {
        buffer[0] = (byte)OpType.RestoreContext;
        return 1;
    }

    /// <summary>
    /// Writes ResetContext operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteResetContext(Span<byte> buffer)
    {
        buffer[0] = (byte)OpType.ResetContext;
        return 1;
    }

    #endregion

    #region Draw Operations

    /// <summary>
    /// Writes DrawPolygon operation with delta-encoded points.
    /// </summary>
    public static int WriteDrawPolygon(Span<byte> buffer, ReadOnlySpan<SKPoint> points)
    {
        int offset = 0;
        buffer[offset++] = (byte)OpType.DrawPolygon;
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)points.Length);

        if (points.Length > 0)
        {
            int firstX = (int)points[0].X;
            int firstY = (int)points[0].Y;
            offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), firstX);
            offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), firstY);

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

        return offset;
    }

    /// <summary>
    /// Writes DrawText operation.
    /// </summary>
    public static int WriteDrawText(Span<byte> buffer, string text, int x, int y)
    {
        int offset = 0;
        buffer[offset++] = (byte)OpType.DrawText;
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), x);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), y);

        var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)textBytes.Length);
        textBytes.CopyTo(buffer.Slice(offset));
        offset += textBytes.Length;

        return offset;
    }

    /// <summary>
    /// Writes DrawCircle operation.
    /// </summary>
    public static int WriteDrawCircle(Span<byte> buffer, int centerX, int centerY, int radius)
    {
        int offset = 0;
        buffer[offset++] = (byte)OpType.DrawCircle;
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), centerX);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), centerY);
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)radius);
        return offset;
    }

    /// <summary>
    /// Writes DrawRect operation.
    /// </summary>
    public static int WriteDrawRect(Span<byte> buffer, int x, int y, int width, int height)
    {
        int offset = 0;
        buffer[offset++] = (byte)OpType.DrawRect;
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), x);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), y);
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)width);
        offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)height);
        return offset;
    }

    /// <summary>
    /// Writes DrawLine operation.
    /// </summary>
    public static int WriteDrawLine(Span<byte> buffer, int x1, int y1, int x2, int y2)
    {
        int offset = 0;
        buffer[offset++] = (byte)OpType.DrawLine;
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), x1);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), y1);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), x2);
        offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), y2);
        return offset;
    }

    #endregion

    #region Property Encoding

    private static int WriteProperty(Span<byte> buffer, PropertyId id, object value)
    {
        buffer[0] = (byte)id;
        int offset = 1;

        switch (id)
        {
            case PropertyId.Stroke:
            case PropertyId.Fill:
            case PropertyId.FontColor:
                var color = (RgbColor)value;
                buffer[offset++] = color.R;
                buffer[offset++] = color.G;
                buffer[offset++] = color.B;
                buffer[offset++] = color.A;
                break;

            case PropertyId.Thickness:
            case PropertyId.FontSize:
                offset += BinaryEncoding.WriteVarint(buffer.Slice(offset), (uint)(int)value);
                break;

            case PropertyId.Offset:
                var offsetPoint = (SKPoint)value;
                offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), (int)offsetPoint.X);
                offset += BinaryEncoding.WriteSignedVarint(buffer.Slice(offset), (int)offsetPoint.Y);
                break;

            case PropertyId.Rotation:
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset), (float)value);
                offset += 4;
                break;

            case PropertyId.Scale:
            case PropertyId.Skew:
                var point = (SKPoint)value;
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset), point.X);
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset + 4), point.Y);
                offset += 8;
                break;

            case PropertyId.Matrix:
                var matrix = (SKMatrix)value;
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset), matrix.ScaleX);
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset + 4), matrix.SkewX);
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset + 8), matrix.TransX);
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset + 12), matrix.SkewY);
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset + 16), matrix.ScaleY);
                BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(offset + 20), matrix.TransY);
                offset += 24;
                break;
        }

        return offset;
    }

    #endregion
}
