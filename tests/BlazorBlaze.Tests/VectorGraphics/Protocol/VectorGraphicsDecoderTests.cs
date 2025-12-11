using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using NSubstitute;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.Tests.VectorGraphics.Protocol;

public class VectorGraphicsDecoderTests
{
    private readonly VectorGraphicsDecoder _decoder;
    private readonly ICanvas _mockCanvas;

    public VectorGraphicsDecoderTests()
    {
        _decoder = new VectorGraphicsDecoder(VectorGraphicsOptions.Default);
        _mockCanvas = Substitute.For<ICanvas>();
    }

    #region Frame Header Tests

    [Fact]
    public void Decode_InsufficientData_ReturnsNeedMoreData()
    {
        var smallBuffer = new byte[] { 0x00, 0x01, 0x02 }; // Less than 11 bytes

        var result = _decoder.Decode(smallBuffer, _mockCanvas);

        result.Success.Should().BeFalse();
        result.BytesConsumed.Should().Be(0);
        result.FrameNumber.Should().BeNull();
    }

    [Fact]
    public void Decode_MinimalValidFrame_DecodesSuccessfully()
    {
        // Build a minimal valid frame:
        // FrameType (1) + FrameId (8) + LayerId (1) + ObjectCount (1) + EndMarker (2) = 13 bytes
        var buffer = new byte[13];
        int offset = 0;

        // Frame type: 0x00 (master frame)
        buffer[offset++] = 0x00;

        // Frame ID: 12345 (little endian)
        ulong frameId = 12345;
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), frameId);
        offset += 8;

        // Layer ID: 0
        buffer[offset++] = 0x00;

        // Object count: 0 (varint)
        buffer[offset++] = 0x00;

        // End marker: 0xFF 0xFF
        buffer[offset++] = 0xFF;
        buffer[offset++] = 0xFF;

        var result = _decoder.Decode(buffer.AsSpan(0, offset), _mockCanvas);

        result.Success.Should().BeTrue();
        result.FrameNumber.Should().Be(frameId);
        result.BytesConsumed.Should().Be(offset);
    }

    [Fact]
    public void Decode_CallsBeginAndEnd_OnCanvas()
    {
        var buffer = CreateMinimalFrame(frameId: 100, layerId: 5);

        _decoder.Decode(buffer, _mockCanvas);

        _mockCanvas.Received(1).Begin(100, 5);
        _mockCanvas.Received(1).End(5);
    }

    #endregion

    #region Polygon Decoding Tests

    [Fact]
    public void Decode_PolygonObject_CallsDrawPolygon()
    {
        var buffer = CreateFrameWithPolygon(
            frameId: 1,
            layerId: 0,
            points: new[] { (100, 100), (200, 100), (200, 200), (100, 200) },
            strokeColor: new RgbColor(255, 0, 0));

        // Use a real SkiaCanvas instead of mock since ReadOnlySpan<SKPoint> can't be mocked
        var skiaCanvas = new TestCanvas();
        _decoder.Decode(buffer, skiaCanvas);

        skiaCanvas.DrawPolygonCalls.Should().Be(1);
        skiaCanvas.LastPolygonColor!.Value.R.Should().Be(255);
    }

    [Fact]
    public void Decode_PolygonWithDeltaEncoding_DecodesCorrectPoints()
    {
        var originalPoints = new[] { (100, 100), (102, 101), (105, 103), (100, 100) };
        var buffer = CreateFrameWithPolygon(frameId: 1, layerId: 0, points: originalPoints);

        var testCanvas = new TestCanvas();
        _decoder.Decode(buffer, testCanvas);

        testCanvas.CapturedPoints.Should().NotBeNull();
        testCanvas.CapturedPoints.Should().HaveCount(originalPoints.Length);

        for (int i = 0; i < originalPoints.Length; i++)
        {
            testCanvas.CapturedPoints![i].X.Should().Be(originalPoints[i].Item1);
            testCanvas.CapturedPoints![i].Y.Should().Be(originalPoints[i].Item2);
        }
    }

    #endregion

    #region Text Decoding Tests

    [Fact]
    public void Decode_TextObject_CallsDrawText()
    {
        var buffer = CreateFrameWithText(
            frameId: 1,
            layerId: 0,
            text: "Hello",
            x: 50,
            y: 100,
            fontSize: 16);

        var testCanvas = new TestCanvas();
        _decoder.Decode(buffer, testCanvas);

        testCanvas.DrawTextCalls.Should().Be(1);
        testCanvas.LastText.Should().Be("Hello");
        testCanvas.LastTextX.Should().Be(50);
        testCanvas.LastTextY.Should().Be(100);
        testCanvas.LastFontSize.Should().Be(16);
    }

    [Fact]
    public void Decode_TextWithUtf8_HandlesSpecialCharacters()
    {
        var buffer = CreateFrameWithText(
            frameId: 1,
            layerId: 0,
            text: "Caf\u00e9",  // UTF-8 encoded
            x: 10,
            y: 20,
            fontSize: 12);

        var testCanvas = new TestCanvas();
        _decoder.Decode(buffer, testCanvas);

        testCanvas.LastText.Should().Be("Caf\u00e9");
    }

    #endregion

    #region Layer Filtering Tests

    [Fact]
    public void Decode_FilteredLayer_SkipsDrawCalls()
    {
        var options = new VectorGraphicsOptions { FilteredLayers = new[] { 1, 2 } };
        var decoder = new VectorGraphicsDecoder(options);

        // Frame for layer 0 (not in filtered list)
        var buffer = CreateFrameWithPolygon(frameId: 1, layerId: 0, points: new[] { (0, 0), (100, 100) });

        var testCanvas = new TestCanvas();
        decoder.Decode(buffer, testCanvas);

        // Should NOT call Begin/End/DrawPolygon for filtered-out layer
        testCanvas.BeginCalls.Should().Be(0);
        testCanvas.EndCalls.Should().Be(0);
        testCanvas.DrawPolygonCalls.Should().Be(0);
    }

    [Fact]
    public void Decode_MatchingFilteredLayer_CallsDrawMethods()
    {
        var options = new VectorGraphicsOptions { FilteredLayers = new[] { 1, 2 } };
        var decoder = new VectorGraphicsDecoder(options);

        var buffer = CreateFrameWithPolygon(frameId: 1, layerId: 1, points: new[] { (0, 0), (100, 100) });

        var testCanvas = new TestCanvas();
        decoder.Decode(buffer, testCanvas);

        testCanvas.BeginCalls.Should().Be(1);
        testCanvas.EndCalls.Should().Be(1);
    }

    #endregion

    #region DrawContext Tests

    [Fact]
    public void Decode_ContextWithStroke_PassesColorToDrawPolygon()
    {
        var buffer = CreateFrameWithPolygon(
            frameId: 1,
            layerId: 0,
            points: new[] { (0, 0), (100, 100) },
            strokeColor: new RgbColor(255, 128, 64, 200));

        var testCanvas = new TestCanvas();
        _decoder.Decode(buffer, testCanvas);

        testCanvas.LastPolygonColor.Should().NotBeNull();
        testCanvas.LastPolygonColor!.Value.R.Should().Be(255);
        testCanvas.LastPolygonColor!.Value.G.Should().Be(128);
        testCanvas.LastPolygonColor!.Value.B.Should().Be(64);
        testCanvas.LastPolygonColor!.Value.A.Should().Be(200);
    }

    [Fact]
    public void Decode_ContextWithThickness_PassesThicknessToDrawPolygon()
    {
        var buffer = CreateFrameWithPolygon(
            frameId: 1,
            layerId: 0,
            points: new[] { (0, 0), (100, 100) },
            thickness: 5);

        var testCanvas = new TestCanvas();
        _decoder.Decode(buffer, testCanvas);

        testCanvas.LastPolygonWidth.Should().Be(5);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_CanBeCalledMultipleTimes()
    {
        _decoder.Reset();
        _decoder.Reset();
        _decoder.Reset();
        // Should not throw
    }

    #endregion

    #region Helper Methods

    private static byte[] CreateMinimalFrame(ulong frameId, byte layerId)
    {
        var buffer = new byte[13];
        int offset = 0;

        buffer[offset++] = 0x00; // Frame type
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), frameId);
        offset += 8;
        buffer[offset++] = layerId;
        buffer[offset++] = 0x00; // Object count
        buffer[offset++] = 0xFF; // End marker
        buffer[offset++] = 0xFF;

        return buffer;
    }

    private static byte[] CreateFrameWithPolygon(
        ulong frameId,
        byte layerId,
        (int X, int Y)[] points,
        RgbColor? strokeColor = null,
        ushort? thickness = null)
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Frame header
        buffer[offset++] = 0x00; // Frame type (master)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), frameId);
        offset += 8;
        buffer[offset++] = layerId;

        // Object count: 1
        buffer[offset++] = 0x01;

        // Object type: Polygon (1)
        buffer[offset++] = 0x01;

        // Point count (varint)
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), (uint)points.Length);

        // First point (absolute, zigzag encoded)
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[0].X);
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[0].Y);

        // Remaining points (delta encoded)
        int lastX = points[0].X;
        int lastY = points[0].Y;
        for (int i = 1; i < points.Length; i++)
        {
            offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[i].X - lastX);
            offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[i].Y - lastY);
            lastX = points[i].X;
            lastY = points[i].Y;
        }

        // Context
        byte contextFlags = 0;
        if (strokeColor.HasValue) contextFlags |= 0x01;
        if (thickness.HasValue) contextFlags |= 0x04;

        buffer[offset++] = contextFlags;

        if (strokeColor.HasValue)
        {
            buffer[offset++] = strokeColor.Value.R;
            buffer[offset++] = strokeColor.Value.G;
            buffer[offset++] = strokeColor.Value.B;
            buffer[offset++] = strokeColor.Value.A;
        }

        if (thickness.HasValue)
        {
            offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), thickness.Value);
        }

        // End marker
        buffer[offset++] = 0xFF;
        buffer[offset++] = 0xFF;

        return buffer.AsSpan(0, offset).ToArray();
    }

    private static byte[] CreateFrameWithText(
        ulong frameId,
        byte layerId,
        string text,
        int x,
        int y,
        ushort fontSize)
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Frame header
        buffer[offset++] = 0x00; // Frame type (master)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), frameId);
        offset += 8;
        buffer[offset++] = layerId;

        // Object count: 1
        buffer[offset++] = 0x01;

        // Object type: Text (2)
        buffer[offset++] = 0x02;

        // X, Y (zigzag encoded)
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), x);
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), y);

        // Text length and UTF8 bytes
        var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), (uint)textBytes.Length);
        textBytes.CopyTo(buffer.AsSpan(offset));
        offset += textBytes.Length;

        // Context with font size
        byte contextFlags = 0x08; // HasFontSize
        buffer[offset++] = contextFlags;
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), fontSize);

        // End marker
        buffer[offset++] = 0xFF;
        buffer[offset++] = 0xFF;

        return buffer.AsSpan(0, offset).ToArray();
    }

    #endregion
}

/// <summary>
/// Test implementation of ICanvas that captures all draw calls for verification.
/// Used because ReadOnlySpan&lt;SKPoint&gt; cannot be mocked with NSubstitute.
/// </summary>
internal class TestCanvas : ICanvas
{
    public byte LayerId { get; set; }
    public object Sync { get; } = new object();

    public int BeginCalls { get; private set; }
    public int EndCalls { get; private set; }
    public int DrawPolygonCalls { get; private set; }
    public int DrawTextCalls { get; private set; }
    public int DrawRectangleCalls { get; private set; }

    public SKPoint[]? CapturedPoints { get; private set; }
    public RgbColor? LastPolygonColor { get; private set; }
    public int LastPolygonWidth { get; private set; }

    public string? LastText { get; private set; }
    public int LastTextX { get; private set; }
    public int LastTextY { get; private set; }
    public int LastFontSize { get; private set; }

    public void Begin(ulong frameNr, byte? layerId = null)
    {
        BeginCalls++;
    }

    public void End(byte? layerId = null)
    {
        EndCalls++;
    }

    public void DrawPolygon(ReadOnlySpan<SKPoint> points, RgbColor? color = null, int width = 1, byte? layerId = null)
    {
        DrawPolygonCalls++;
        CapturedPoints = points.ToArray();
        LastPolygonColor = color;
        LastPolygonWidth = width;
    }

    public void DrawRectangle(System.Drawing.Rectangle rect, RgbColor? color = null, byte? layerId = null)
    {
        DrawRectangleCalls++;
    }

    public void DrawText(string text, int x = 0, int y = 0, int size = 12, RgbColor? color = null, byte? layerId = null)
    {
        DrawTextCalls++;
        LastText = text;
        LastTextX = x;
        LastTextY = y;
        LastFontSize = size;
    }
}
