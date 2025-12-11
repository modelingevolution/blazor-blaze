using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.Tests.VectorGraphics.Protocol;

public class RenderingCallbackV2Tests : IDisposable
{
    private readonly LayerManager _layerManager;
    private readonly RenderingCallbackV2 _callback;
    private readonly VectorGraphicsDecoderV2 _decoder;

    public RenderingCallbackV2Tests()
    {
        _layerManager = new LayerManager(100, 100);
        _callback = new RenderingCallbackV2(_layerManager);
        _decoder = new VectorGraphicsDecoderV2();
    }

    public void Dispose()
    {
        _layerManager.Dispose();
    }

    #region LayerManager Tests

    [Fact]
    public void LayerManager_CreatesLayerOnDemand()
    {
        var layer = _layerManager.GetLayer(5);

        layer.Should().NotBeNull();
        layer.LayerId.Should().Be(5);
    }

    [Fact]
    public void LayerManager_ReturnsSameLayerForSameId()
    {
        var layer1 = _layerManager.GetLayer(1);
        var layer2 = _layerManager.GetLayer(1);

        layer1.Should().BeSameAs(layer2);
    }

    [Fact]
    public void LayerManager_GetLayerIds_ReturnsInSortedOrder()
    {
        _layerManager.GetLayer(5);
        _layerManager.GetLayer(1);
        _layerManager.GetLayer(3);

        var ids = _layerManager.GetLayerIds().ToList();

        ids.Should().BeEquivalentTo(new byte[] { 1, 3, 5 });
        ids.Should().BeInAscendingOrder();
    }

    [Fact]
    public void LayerManager_ClearLayer_ClearsSpecificLayer()
    {
        var layer = _layerManager.GetLayer(0);

        // Draw something
        using var paint = new SKPaint { Color = SKColors.Red };
        layer.Canvas.DrawRect(0, 0, 50, 50, paint);

        // Verify pixel is drawn
        using var bitmap1 = new SKBitmap(100, 100);
        using var canvas1 = new SKCanvas(bitmap1);
        layer.DrawTo(canvas1);
        bitmap1.GetPixel(25, 25).Red.Should().Be(255);

        // Clear
        _layerManager.ClearLayer(0);

        // Verify pixel is now transparent
        using var bitmap2 = new SKBitmap(100, 100);
        using var canvas2 = new SKCanvas(bitmap2);
        bitmap2.Erase(SKColors.Transparent);
        layer.DrawTo(canvas2);
        bitmap2.GetPixel(25, 25).Alpha.Should().Be(0);
    }

    #endregion

    #region Rendering Integration Tests

    [Fact]
    public void Render_DrawPolygon_DrawsPixels()
    {
        var points = new SKPoint[] { new(10, 10), new(50, 10), new(50, 50), new(10, 50) };

        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 0, 0)); // Red
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer, _callback);

        // Composite to a test bitmap
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Verify that red pixels exist on the polygon border
        // Check a point on the top edge
        var topEdgePixel = bitmap.GetPixel(30, 10);
        topEdgePixel.Red.Should().Be(255);
    }

    [Fact]
    public void Render_DrawText_DrawsText()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetFontColor(ops, new RgbColor(0, 0, 255)); // Blue
            offset += VectorGraphicsEncoderV2.WriteSetFontSize(ops.Slice(offset), 20);
            offset += VectorGraphicsEncoderV2.WriteDrawText(ops.Slice(offset), "Hi", 10, 50);
            return offset;
        }, opCount: 3);

        _decoder.Decode(buffer, _callback);

        // Composite to a test bitmap
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Verify that some blue pixels exist (text was rendered)
        bool hasBluePixel = false;
        for (int y = 30; y < 60; y++)
        {
            for (int x = 5; x < 50; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Blue > 200 && pixel.Red < 100 && pixel.Green < 100)
                {
                    hasBluePixel = true;
                    break;
                }
            }
            if (hasBluePixel) break;
        }
        hasBluePixel.Should().BeTrue("Text should have rendered blue pixels");
    }

    [Fact]
    public void Render_DrawCircle_DrawsCircle()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(0, 255, 0)); // Green
            offset += VectorGraphicsEncoderV2.WriteDrawCircle(ops.Slice(offset), 50, 50, 20);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer, _callback);

        // Composite to a test bitmap
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Verify that green pixels exist on the circle edge (top)
        var topPixel = bitmap.GetPixel(50, 30);
        topPixel.Green.Should().Be(255);
    }

    [Fact]
    public void Render_DrawRect_DrawsRectangle()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 255, 0)); // Yellow
            offset += VectorGraphicsEncoderV2.WriteDrawRect(ops.Slice(offset), 20, 20, 40, 30);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer, _callback);

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        _layerManager.Composite(canvas);

        // Verify yellow-ish pixels on rectangle edges
        // Due to anti-aliasing/blending, exact colors may vary, but R and G should be significantly higher than B
        var topEdge = bitmap.GetPixel(40, 20);
        topEdge.Red.Should().BeGreaterThan(100);
        topEdge.Green.Should().BeGreaterThan(100);
        (topEdge.Red + topEdge.Green).Should().BeGreaterThan(topEdge.Blue * 3); // Yellow-ish
    }

    [Fact]
    public void Render_DrawLine_DrawsLine()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(128, 0, 128)); // Purple
            offset += VectorGraphicsEncoderV2.WriteDrawLine(ops.Slice(offset), 10, 10, 90, 90);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer, _callback);

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Verify purple pixel on the line (center)
        var centerPixel = bitmap.GetPixel(50, 50);
        centerPixel.Red.Should().Be(128);
        centerPixel.Blue.Should().Be(128);
    }

    #endregion

    #region Transform Tests

    [Fact]
    public void Render_WithOffset_AppliesTranslation()
    {
        var points = new SKPoint[] { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };

        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 0, 0));
            offset += VectorGraphicsEncoderV2.WriteSetOffset(ops.Slice(offset), 50, 50); // Translate
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 3);

        _decoder.Decode(buffer, _callback);

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Polygon at (0,0) translated by (50,50) should have pixels near (50,50)
        var translatedPixel = bitmap.GetPixel(55, 50);
        translatedPixel.Red.Should().Be(255);

        // Original position should be white
        var originalPixel = bitmap.GetPixel(5, 0);
        originalPixel.Should().Be(SKColors.White);
    }

    #endregion

    #region Multi-Layer Tests

    [Fact]
    public void Render_MultipleLayers_CompositesInOrder()
    {
        // Layer 0: Red square at (10,10)
        var buffer1 = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 0, 0));
            offset += VectorGraphicsEncoderV2.WriteDrawRect(ops.Slice(offset), 10, 10, 30, 30);
            return offset;
        }, opCount: 2);

        // Layer 1: Green square overlapping at (20,20)
        var buffer2 = CreateMessage2Layers(
            frameId: 2,
            layer0FrameType: FrameType.Remain, // Keep layer 0
            layer1Ops: ops =>
            {
                int offset = 0;
                offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(0, 255, 0));
                offset += VectorGraphicsEncoderV2.WriteDrawRect(ops.Slice(offset), 20, 20, 30, 30);
                return offset;
            },
            layer1OpCount: 2);

        _decoder.Decode(buffer1, _callback);
        _decoder.Decode(buffer2, _callback);

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Check layer 0's exclusive area (top-left of red square)
        var redArea = bitmap.GetPixel(10, 10);
        redArea.Red.Should().Be(255);

        // Check layer 1's exclusive area (bottom-right of green square)
        var greenArea = bitmap.GetPixel(49, 49);
        greenArea.Green.Should().Be(255);
    }

    [Fact]
    public void Render_FrameTypeMaster_ClearsLayerBeforeDrawing()
    {
        // First frame: draw red rectangle
        var buffer1 = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 0, 0));
            offset += VectorGraphicsEncoderV2.WriteDrawRect(ops.Slice(offset), 10, 10, 20, 20);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer1, _callback);

        // Second frame: Master type clears and draws green rectangle in different location
        var buffer2 = CreateMessageWithOps(frameId: 2, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(0, 255, 0));
            offset += VectorGraphicsEncoderV2.WriteDrawRect(ops.Slice(offset), 60, 60, 20, 20);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer2, _callback);

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Red rectangle area should now be white (cleared)
        var oldRedArea = bitmap.GetPixel(10, 10);
        oldRedArea.Should().Be(SKColors.White);

        // Green rectangle should be present
        var greenArea = bitmap.GetPixel(60, 60);
        greenArea.Green.Should().Be(255);
    }

    [Fact]
    public void Render_FrameTypeRemain_KeepsPreviousContent()
    {
        // First frame: draw red rectangle
        var buffer1 = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 0, 0));
            offset += VectorGraphicsEncoderV2.WriteDrawRect(ops.Slice(offset), 10, 10, 20, 20);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer1, _callback);

        // Second frame: Remain type keeps layer content
        var buffer2 = CreateMinimalMessage(frameId: 2, layerCount: 1, layerId: 0, FrameType.Remain);

        _decoder.Decode(buffer2, _callback);

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Red rectangle should still be present
        var redArea = bitmap.GetPixel(10, 10);
        redArea.Red.Should().Be(255);
    }

    [Fact]
    public void Render_FrameTypeClear_ClearsToTransparent()
    {
        // First frame: draw red rectangle
        var buffer1 = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 0, 0));
            offset += VectorGraphicsEncoderV2.WriteDrawRect(ops.Slice(offset), 10, 10, 20, 20);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer1, _callback);

        // Second frame: Clear type makes layer transparent
        var buffer2 = CreateMinimalMessage(frameId: 2, layerCount: 1, layerId: 0, FrameType.Clear);

        _decoder.Decode(buffer2, _callback);

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        _layerManager.Composite(canvas);

        // Layer should be transparent, so background white shows through
        var clearedArea = bitmap.GetPixel(10, 10);
        clearedArea.Should().Be(SKColors.White);
    }

    #endregion

    #region Helper Methods

    private static byte[] CreateMessageWithOps(ulong frameId, byte layerId, Func<Span<byte>, int> writeOps, uint opCount = 1)
    {
        var buffer = new byte[4096];
        var opsBuffer = new byte[2048];
        int offset = 0;

        // Write ops to temp buffer first
        int opsLength = writeOps(opsBuffer);

        // Header
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer, frameId, 1);

        // Layer header (Master)
        buffer[offset++] = layerId;
        buffer[offset++] = (byte)FrameType.Master;
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), opCount);

        // Copy ops
        opsBuffer.AsSpan(0, opsLength).CopyTo(buffer.AsSpan(offset));
        offset += opsLength;

        // End marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        return buffer.AsSpan(0, offset).ToArray();
    }

    private static byte[] CreateMinimalMessage(ulong frameId, byte layerCount, byte layerId, FrameType frameType)
    {
        var buffer = new byte[256];
        int offset = 0;

        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer, frameId, layerCount);

        buffer[offset++] = layerId;
        buffer[offset++] = (byte)frameType;

        if (frameType == FrameType.Master)
        {
            offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), 0);
        }

        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        return buffer.AsSpan(0, offset).ToArray();
    }

    private static byte[] CreateMessage2Layers(
        ulong frameId,
        FrameType layer0FrameType,
        Func<Span<byte>, int> layer1Ops,
        uint layer1OpCount)
    {
        var buffer = new byte[4096];
        var opsBuffer = new byte[2048];
        int offset = 0;

        // Header with 2 layers
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer, frameId, 2);

        // Layer 0
        buffer[offset++] = 0;
        buffer[offset++] = (byte)layer0FrameType;
        if (layer0FrameType == FrameType.Master)
        {
            offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), 0);
        }

        // Layer 1 (Master with ops)
        buffer[offset++] = 1;
        buffer[offset++] = (byte)FrameType.Master;
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), layer1OpCount);

        int opsLength = layer1Ops(opsBuffer);
        opsBuffer.AsSpan(0, opsLength).CopyTo(buffer.AsSpan(offset));
        offset += opsLength;

        // End marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        return buffer.AsSpan(0, offset).ToArray();
    }

    #endregion
}
