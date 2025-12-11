using System.Buffers.Binary;
using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using SkiaSharp;

namespace BlazorBlaze.Tests.VectorGraphics.Protocol;

public class VectorGraphicsEncoderV2Tests
{
    #region Message Header Tests

    [Fact]
    public void WriteMessageHeader_EncodesFrameIdAndLayerCount()
    {
        var buffer = new byte[20];
        ulong frameId = 0x123456789ABCDEF0;
        byte layerCount = 3;

        int written = VectorGraphicsEncoderV2.WriteMessageHeader(buffer, frameId, layerCount);

        written.Should().Be(9);
        BinaryPrimitives.ReadUInt64LittleEndian(buffer).Should().Be(frameId);
        buffer[8].Should().Be(layerCount);
    }

    [Fact]
    public void WriteEndMarker_WritesCorrectBytes()
    {
        var buffer = new byte[10];

        int written = VectorGraphicsEncoderV2.WriteEndMarker(buffer);

        written.Should().Be(2);
        buffer[0].Should().Be(0xFF);
        buffer[1].Should().Be(0xFF);
    }

    #endregion

    #region Layer Block Tests

    [Fact]
    public void WriteLayerMaster_EncodesLayerIdFrameTypeAndOpCount()
    {
        var buffer = new byte[20];
        byte layerId = 2;
        uint opCount = 100;

        int written = VectorGraphicsEncoderV2.WriteLayerMaster(buffer, layerId, opCount);

        // LayerId(1) + FrameType(1) + OpCount(varint)
        written.Should().BeGreaterThanOrEqualTo(3);
        buffer[0].Should().Be(layerId);
        buffer[1].Should().Be((byte)FrameType.Master);

        // Verify opCount can be read back
        int consumed = BinaryEncoding.ReadVarint(buffer.AsSpan(2), out uint decodedOpCount);
        decodedOpCount.Should().Be(opCount);
    }

    [Fact]
    public void WriteLayerRemain_EncodesMinimalBlock()
    {
        var buffer = new byte[10];
        byte layerId = 1;

        int written = VectorGraphicsEncoderV2.WriteLayerRemain(buffer, layerId);

        written.Should().Be(2);
        buffer[0].Should().Be(layerId);
        buffer[1].Should().Be((byte)FrameType.Remain);
    }

    [Fact]
    public void WriteLayerClear_EncodesMinimalBlock()
    {
        var buffer = new byte[10];
        byte layerId = 0;

        int written = VectorGraphicsEncoderV2.WriteLayerClear(buffer, layerId);

        written.Should().Be(2);
        buffer[0].Should().Be(layerId);
        buffer[1].Should().Be((byte)FrameType.Clear);
    }

    #endregion

    #region Context Operation Tests

    [Fact]
    public void WriteSetStroke_EncodesColorCorrectly()
    {
        var buffer = new byte[20];
        var color = new RgbColor(255, 128, 64, 200);

        int written = VectorGraphicsEncoderV2.WriteSetStroke(buffer, color);

        written.Should().Be(7);
        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[1].Should().Be(1); // Field count
        buffer[2].Should().Be((byte)PropertyId.Stroke);
        buffer[3].Should().Be(255); // R
        buffer[4].Should().Be(128); // G
        buffer[5].Should().Be(64);  // B
        buffer[6].Should().Be(200); // A
    }

    [Fact]
    public void WriteSetFill_EncodesColorCorrectly()
    {
        var buffer = new byte[20];
        var color = new RgbColor(0, 255, 0);

        int written = VectorGraphicsEncoderV2.WriteSetFill(buffer, color);

        written.Should().Be(7);
        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[1].Should().Be(1);
        buffer[2].Should().Be((byte)PropertyId.Fill);
        buffer[3].Should().Be(0);   // R
        buffer[4].Should().Be(255); // G
        buffer[5].Should().Be(0);   // B
        buffer[6].Should().Be(255); // A (default)
    }

    [Fact]
    public void WriteSetThickness_EncodesVarint()
    {
        var buffer = new byte[20];
        int thickness = 5;

        int written = VectorGraphicsEncoderV2.WriteSetThickness(buffer, thickness);

        written.Should().Be(4); // OpType(1) + FieldCount(1) + PropId(1) + Varint(1)
        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[1].Should().Be(1);
        buffer[2].Should().Be((byte)PropertyId.Thickness);
        buffer[3].Should().Be(5);
    }

    [Fact]
    public void WriteSetFontSize_EncodesCorrectly()
    {
        var buffer = new byte[20];
        int fontSize = 24;

        int written = VectorGraphicsEncoderV2.WriteSetFontSize(buffer, fontSize);

        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[2].Should().Be((byte)PropertyId.FontSize);

        // Read back the varint
        int consumed = BinaryEncoding.ReadVarint(buffer.AsSpan(3), out uint decodedSize);
        decodedSize.Should().Be(24);
    }

    [Fact]
    public void WriteSetOffset_EncodesZigzagVarint()
    {
        var buffer = new byte[20];

        int written = VectorGraphicsEncoderV2.WriteSetOffset(buffer, 100f, -50f);

        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[1].Should().Be(1);
        buffer[2].Should().Be((byte)PropertyId.Offset);

        // Read back the zigzag varints
        int offset = 3;
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int x);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int y);
        x.Should().Be(100);
        y.Should().Be(-50);
    }

    [Fact]
    public void WriteSetRotation_EncodesFloat()
    {
        var buffer = new byte[20];
        float degrees = 45.5f;

        int written = VectorGraphicsEncoderV2.WriteSetRotation(buffer, degrees);

        written.Should().Be(7);
        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[2].Should().Be((byte)PropertyId.Rotation);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(3)).Should().Be(degrees);
    }

    [Fact]
    public void WriteSetScale_EncodesFloatPair()
    {
        var buffer = new byte[20];
        float scaleX = 1.5f;
        float scaleY = 2.0f;

        int written = VectorGraphicsEncoderV2.WriteSetScale(buffer, scaleX, scaleY);

        written.Should().Be(11);
        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[2].Should().Be((byte)PropertyId.Scale);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(3)).Should().Be(scaleX);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(7)).Should().Be(scaleY);
    }

    [Fact]
    public void WriteSetSkew_EncodesFloatPair()
    {
        var buffer = new byte[20];
        float skewX = 0.1f;
        float skewY = -0.2f;

        int written = VectorGraphicsEncoderV2.WriteSetSkew(buffer, skewX, skewY);

        written.Should().Be(11);
        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[2].Should().Be((byte)PropertyId.Skew);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(3)).Should().Be(skewX);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(7)).Should().Be(skewY);
    }

    [Fact]
    public void WriteSetMatrix_EncodesFullMatrix()
    {
        var buffer = new byte[50];
        var matrix = SKMatrix.CreateRotationDegrees(45);

        int written = VectorGraphicsEncoderV2.WriteSetMatrix(buffer, matrix);

        written.Should().Be(27);
        buffer[0].Should().Be((byte)OpType.SetContext);
        buffer[2].Should().Be((byte)PropertyId.Matrix);

        // Read back the matrix components
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(3)).Should().Be(matrix.ScaleX);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(7)).Should().Be(matrix.SkewX);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(11)).Should().Be(matrix.TransX);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(15)).Should().Be(matrix.SkewY);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(19)).Should().Be(matrix.ScaleY);
        BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(23)).Should().Be(matrix.TransY);
    }

    [Fact]
    public void WriteSaveContext_WritesSingleByte()
    {
        var buffer = new byte[10];

        int written = VectorGraphicsEncoderV2.WriteSaveContext(buffer);

        written.Should().Be(1);
        buffer[0].Should().Be((byte)OpType.SaveContext);
    }

    [Fact]
    public void WriteRestoreContext_WritesSingleByte()
    {
        var buffer = new byte[10];

        int written = VectorGraphicsEncoderV2.WriteRestoreContext(buffer);

        written.Should().Be(1);
        buffer[0].Should().Be((byte)OpType.RestoreContext);
    }

    [Fact]
    public void WriteResetContext_WritesSingleByte()
    {
        var buffer = new byte[10];

        int written = VectorGraphicsEncoderV2.WriteResetContext(buffer);

        written.Should().Be(1);
        buffer[0].Should().Be((byte)OpType.ResetContext);
    }

    #endregion

    #region Draw Operation Tests

    [Fact]
    public void WriteDrawPolygon_EncodesTriangle()
    {
        var buffer = new byte[100];
        var points = new SKPoint[]
        {
            new(100, 100),
            new(150, 200),
            new(50, 200)
        };

        int written = VectorGraphicsEncoderV2.WriteDrawPolygon(buffer, points);

        buffer[0].Should().Be((byte)OpType.DrawPolygon);

        // Read point count
        int offset = 1;
        offset += BinaryEncoding.ReadVarint(buffer.AsSpan(offset), out uint pointCount);
        pointCount.Should().Be(3);

        // Read first point (absolute)
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int x);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int y);
        x.Should().Be(100);
        y.Should().Be(100);

        // Read second point (delta)
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int dx1);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int dy1);
        (x + dx1).Should().Be(150);
        (y + dy1).Should().Be(200);
    }

    [Fact]
    public void WriteDrawPolygon_EmptyPoints_WritesZeroCount()
    {
        var buffer = new byte[20];
        var points = Array.Empty<SKPoint>();

        int written = VectorGraphicsEncoderV2.WriteDrawPolygon(buffer, points);

        buffer[0].Should().Be((byte)OpType.DrawPolygon);
        buffer[1].Should().Be(0); // Point count = 0
    }

    [Fact]
    public void WriteDrawText_EncodesPositionAndUtf8()
    {
        var buffer = new byte[100];
        string text = "Hello";
        int x = 50;
        int y = 100;

        int written = VectorGraphicsEncoderV2.WriteDrawText(buffer, text, x, y);

        buffer[0].Should().Be((byte)OpType.DrawText);

        // Read position
        int offset = 1;
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readX);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readY);
        readX.Should().Be(x);
        readY.Should().Be(y);

        // Read text length
        offset += BinaryEncoding.ReadVarint(buffer.AsSpan(offset), out uint textLen);
        textLen.Should().Be((uint)System.Text.Encoding.UTF8.GetByteCount(text));

        // Read text
        var textBytes = buffer.AsSpan(offset, (int)textLen);
        System.Text.Encoding.UTF8.GetString(textBytes).Should().Be(text);
    }

    [Fact]
    public void WriteDrawCircle_EncodesCenterAndRadius()
    {
        var buffer = new byte[50];
        int centerX = 200;
        int centerY = 150;
        int radius = 50;

        int written = VectorGraphicsEncoderV2.WriteDrawCircle(buffer, centerX, centerY, radius);

        buffer[0].Should().Be((byte)OpType.DrawCircle);

        int offset = 1;
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readCenterX);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readCenterY);
        offset += BinaryEncoding.ReadVarint(buffer.AsSpan(offset), out uint readRadius);

        readCenterX.Should().Be(centerX);
        readCenterY.Should().Be(centerY);
        readRadius.Should().Be((uint)radius);
    }

    [Fact]
    public void WriteDrawRect_EncodesPositionAndSize()
    {
        var buffer = new byte[50];
        int x = 10;
        int y = 20;
        int width = 100;
        int height = 50;

        int written = VectorGraphicsEncoderV2.WriteDrawRect(buffer, x, y, width, height);

        buffer[0].Should().Be((byte)OpType.DrawRect);

        int offset = 1;
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readX);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readY);
        offset += BinaryEncoding.ReadVarint(buffer.AsSpan(offset), out uint readWidth);
        offset += BinaryEncoding.ReadVarint(buffer.AsSpan(offset), out uint readHeight);

        readX.Should().Be(x);
        readY.Should().Be(y);
        readWidth.Should().Be((uint)width);
        readHeight.Should().Be((uint)height);
    }

    [Fact]
    public void WriteDrawLine_EncodesStartAndEnd()
    {
        var buffer = new byte[50];
        int x1 = 0;
        int y1 = 0;
        int x2 = 100;
        int y2 = 100;

        int written = VectorGraphicsEncoderV2.WriteDrawLine(buffer, x1, y1, x2, y2);

        buffer[0].Should().Be((byte)OpType.DrawLine);

        int offset = 1;
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readX1);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readY1);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readX2);
        offset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(offset), out int readY2);

        readX1.Should().Be(x1);
        readY1.Should().Be(y1);
        readX2.Should().Be(x2);
        readY2.Should().Be(y2);
    }

    #endregion

    #region Complete Frame Tests

    [Fact]
    public void EncodeCompleteFrame_SingleLayerWithPolygon()
    {
        var buffer = new byte[200];
        ulong frameId = 42;
        byte layerId = 0;
        var points = new SKPoint[]
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100)
        };

        int offset = 0;

        // Message header
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), frameId, 1);

        // Layer block
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), layerId, 2);

        // Set stroke
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), RgbColor.Red);

        // Draw polygon
        offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.AsSpan(offset), points);

        // End marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Verify structure
        int readOffset = 0;

        // Read frame ID
        BinaryPrimitives.ReadUInt64LittleEndian(buffer).Should().Be(frameId);
        readOffset += 8;

        // Read layer count
        buffer[readOffset++].Should().Be(1);

        // Read layer ID
        buffer[readOffset++].Should().Be(layerId);

        // Read frame type
        buffer[readOffset++].Should().Be((byte)FrameType.Master);

        // Read op count
        readOffset += BinaryEncoding.ReadVarint(buffer.AsSpan(readOffset), out uint opCount);
        opCount.Should().Be(2);
    }

    [Fact]
    public void EncodeMultiLayerFrame_KeyframeCompression()
    {
        var buffer = new byte[100];
        ulong frameId = 100;
        int offset = 0;

        // Message header (3 layers)
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), frameId, 3);

        // Layer 0: Remain (background)
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 0);

        // Layer 1: Master (animated content)
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.AsSpan(offset), new SKPoint[] { new(0, 0), new(10, 10) });

        // Layer 2: Remain (UI)
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 2);

        // End marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Verify total size is compact for Remain layers
        // Message header (9) + 2x Remain (4) + Master header (~3) + Polygon (~10) + End marker (2) ~= 28 bytes
        offset.Should().BeLessThan(40);
    }

    [Fact]
    public void EncodeSaveRestorePattern()
    {
        var buffer = new byte[100];
        int offset = 0;

        // Save context
        offset += VectorGraphicsEncoderV2.WriteSaveContext(buffer.AsSpan(offset));

        // Modify context
        offset += VectorGraphicsEncoderV2.WriteSetOffset(buffer.AsSpan(offset), 100, 100);
        offset += VectorGraphicsEncoderV2.WriteSetRotation(buffer.AsSpan(offset), 45);

        // Draw
        offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.AsSpan(offset),
            new SKPoint[] { new(0, 0), new(10, 0), new(10, 10) });

        // Restore context
        offset += VectorGraphicsEncoderV2.WriteRestoreContext(buffer.AsSpan(offset));

        // Verify operation sequence
        buffer[0].Should().Be((byte)OpType.SaveContext);
        // Skip SetOffset and SetRotation operations
        // Find RestoreContext at the end (before polygon data)
        buffer[offset - 1].Should().Be((byte)OpType.RestoreContext);
    }

    #endregion

    #region Delta Encoding Efficiency Tests

    [Fact]
    public void WriteDrawPolygon_SmallDeltas_CompactEncoding()
    {
        var buffer = new byte[100];

        // Points with small increments (typical for smooth curves)
        var points = new SKPoint[10];
        for (int i = 0; i < 10; i++)
        {
            points[i] = new SKPoint(100 + i * 5, 100 + i * 2);
        }

        int written = VectorGraphicsEncoderV2.WriteDrawPolygon(buffer, points);

        // With small deltas (5 and 2), each delta should be 1 byte
        // OpType(1) + PointCount(1) + FirstPoint(~4) + 9 deltas(~18) = ~24 bytes
        written.Should().BeLessThan(30);
    }

    #endregion
}
