using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using NSubstitute;
using SkiaSharp;

using ICanvas = BlazorBlaze.VectorGraphics.Protocol.ICanvas;

namespace ModelingEvolution.BlazorBlaze.Tests.VectorGraphics.Protocol;

public class VectorGraphicsDecoderV2Tests
{
    private readonly VectorGraphicsDecoderV2 _decoder;
    private readonly TestStage _stage;

    public VectorGraphicsDecoderV2Tests()
    {
        _stage = new TestStage();
        _decoder = new VectorGraphicsDecoderV2(_stage);
    }

    #region Message Header Tests

    [Fact]
    public void Decode_InsufficientData_ReturnsNeedMoreData()
    {
        var smallBuffer = new byte[] { 0x00, 0x01, 0x02 };

        var result = _decoder.Decode(smallBuffer);

        result.Success.Should().BeFalse();
        result.BytesConsumed.Should().Be(0);
    }

    [Fact]
    public void Decode_MinimalValidMessage_DecodesSuccessfully()
    {
        var buffer = new byte[256];
        int offset = 0;

        // Header: frameId=12345, layerCount=1
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer, 12345, 1);
        // Layer: layerId=0, Remain (no ops)
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 0);
        // End marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        var result = _decoder.Decode(buffer.AsSpan(0, offset));

        result.Success.Should().BeTrue();
        result.FrameId.Should().Be(12345UL);
        result.LayerCount.Should().Be(1);
        result.BytesConsumed.Should().Be(offset);
    }

    [Fact]
    public void Decode_CallsFrameStartAndEnd()
    {
        var buffer = CreateMinimalMessage(frameId: 100, layerCount: 1, layerId: 5, FrameType.Remain);

        _decoder.Decode(buffer);

        _stage.FrameStartCalls.Should().Be(1);
        _stage.LastFrameId.Should().Be(100UL);
        _stage.FrameEndCalls.Should().Be(1);
    }

    #endregion

    #region Layer Block Tests

    [Fact]
    public void Decode_LayerRemain_DoesNotClearLayer()
    {
        var buffer = CreateMinimalMessage(frameId: 1, layerCount: 1, layerId: 3, FrameType.Remain);

        _decoder.Decode(buffer);

        // Remain type does not clear the layer
        _stage.ClearLayerCalls.Should().Be(0);
    }

    [Fact]
    public void Decode_LayerClear_CallsClearLayer()
    {
        var buffer = CreateMinimalMessage(frameId: 1, layerCount: 1, layerId: 7, FrameType.Clear);

        _decoder.Decode(buffer);

        // Clear type clears the layer
        _stage.ClearLayerCalls.Should().Be(1);
        _stage.LastClearedLayerId.Should().Be(7);
    }

    [Fact]
    public void Decode_LayerMaster_CallsClearLayer()
    {
        var buffer = CreateMinimalMessage(frameId: 1, layerCount: 1, layerId: 5, FrameType.Master);

        _decoder.Decode(buffer);

        // Master type also clears the layer
        _stage.ClearLayerCalls.Should().Be(1);
        _stage.LastClearedLayerId.Should().Be(5);
    }

    [Fact]
    public void Decode_MultipleLayers_ProcessesAllLayers()
    {
        var buffer = new byte[256];
        int offset = 0;

        // Header: 3 layers
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer, 1, 3);
        // Layer 0: Remain (no clear)
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 0);
        // Layer 1: Clear (clears layer)
        offset += VectorGraphicsEncoderV2.WriteLayerClear(buffer.AsSpan(offset), 1);
        // Layer 2: Remain (no clear)
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 2);
        // End marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        var result = _decoder.Decode(buffer.AsSpan(0, offset));

        result.Success.Should().BeTrue();
        result.LayerCount.Should().Be(3);
        // Only layer 1 (Clear) should cause ClearLayer to be called
        _stage.ClearLayerCalls.Should().Be(1);
    }

    #endregion

    #region Context Operations Tests

    [Fact]
    public void Decode_SetStroke_PassesToDrawCall()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 128, 64, 200));
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer);

        // Stroke is passed directly to DrawPolygon
        _stage.DrawPolygonCalls.Should().Be(1);
        _stage.LastPolygonStroke!.Value.R.Should().Be(255);
        _stage.LastPolygonStroke!.Value.G.Should().Be(128);
        _stage.LastPolygonStroke!.Value.B.Should().Be(64);
        _stage.LastPolygonStroke!.Value.A.Should().Be(200);
    }

    [Fact]
    public void Decode_SetThickness_PassesToDrawCall()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetThickness(ops, 5);
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer);

        _stage.LastPolygonThickness.Should().Be(5);
    }

    [Fact]
    public void Decode_SetFontSize_PassesToDrawText()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetFontSize(ops, 24);
            offset += VectorGraphicsEncoderV2.WriteDrawText(ops.Slice(offset), "Test", 10, 20);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer);

        _stage.DrawTextCalls.Should().Be(1);
        var canvas = _stage.GetCanvas(0);
        canvas.LastTextFontSize.Should().Be(24);
    }

    [Fact]
    public void Decode_SetFontColor_PassesToDrawText()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetFontColor(ops, new RgbColor(10, 20, 30, 255));
            offset += VectorGraphicsEncoderV2.WriteDrawText(ops.Slice(offset), "Test", 10, 20);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer);

        var canvas = _stage.GetCanvas(0);
        canvas.LastTextColor!.Value.R.Should().Be(10);
        canvas.LastTextColor!.Value.G.Should().Be(20);
        canvas.LastTextColor!.Value.B.Should().Be(30);
    }

    [Fact]
    public void Decode_SetOffset_CallsSetMatrix()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetOffset(ops, 100, 200);
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer);

        // SetOffset causes SetMatrix to be called with a translation matrix
        _stage.SetMatrixCalls.Should().BeGreaterThan(0);
        _stage.LastMatrix.TransX.Should().Be(100);
        _stage.LastMatrix.TransY.Should().Be(200);
    }

    [Fact]
    public void Decode_SetScale_CallsSetMatrix()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetScale(ops, 2.0f, 1.5f);
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer);

        _stage.SetMatrixCalls.Should().BeGreaterThan(0);
        _stage.LastMatrix.ScaleX.Should().Be(2.0f);
        _stage.LastMatrix.ScaleY.Should().Be(1.5f);
    }

    [Fact]
    public void Decode_SetMatrix_CallsSetMatrixDirectly()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };
        var matrix = new SKMatrix(1.5f, 0.1f, 100f, 0.2f, 2.0f, 200f, 0, 0, 1);
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetMatrix(ops, matrix);
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer);

        _stage.SetMatrixCalls.Should().BeGreaterThan(0);
        _stage.LastMatrix.ScaleX.Should().Be(1.5f);
        _stage.LastMatrix.SkewX.Should().Be(0.1f);
        _stage.LastMatrix.TransX.Should().Be(100f);
        _stage.LastMatrix.SkewY.Should().Be(0.2f);
        _stage.LastMatrix.ScaleY.Should().Be(2.0f);
        _stage.LastMatrix.TransY.Should().Be(200f);
    }

    [Fact]
    public void Decode_SaveContext_CallsCanvasSave()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSaveContext(ops);
        });

        var result = _decoder.Decode(buffer);

        result.Success.Should().BeTrue();
        _stage.SaveCalls.Should().Be(1);
    }

    [Fact]
    public void Decode_RestoreContext_CallsCanvasRestore()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSaveContext(ops);
            offset += VectorGraphicsEncoderV2.WriteRestoreContext(ops.Slice(offset));
            return offset;
        }, opCount: 2);

        var result = _decoder.Decode(buffer);

        result.Success.Should().BeTrue();
        _stage.SaveCalls.Should().Be(1);
        _stage.RestoreCalls.Should().Be(1);
    }

    [Fact]
    public void Decode_ResetContext_ProcessesWithoutError()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteResetContext(ops);
        });

        var result = _decoder.Decode(buffer);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Decode_SaveAndRestore_PreservesStroke()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };

        // Test that context is properly saved and restored
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 0, 0)); // Red
            offset += VectorGraphicsEncoderV2.WriteSaveContext(ops.Slice(offset));
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops.Slice(offset), new RgbColor(0, 255, 0)); // Green
            offset += VectorGraphicsEncoderV2.WriteRestoreContext(ops.Slice(offset));
            // Now draw a polygon - it should use the restored red stroke
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 5);

        _decoder.Decode(buffer);

        // After restore, the polygon should be drawn with the restored red stroke
        _stage.LastPolygonStroke!.Value.R.Should().Be(255);
        _stage.LastPolygonStroke!.Value.G.Should().Be(0);
    }

    #endregion

    #region Draw Operations Tests

    [Fact]
    public void Decode_DrawPolygon_CallsCallback()
    {
        var points = new SKPoint[] { new(100, 100), new(200, 100), new(200, 200), new(100, 200) };

        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawPolygon(ops, points);
        });

        _decoder.Decode(buffer);

        _stage.DrawPolygonCalls.Should().Be(1);
        _stage.LastPolygonPoints.Should().HaveCount(4);
        _stage.LastPolygonPoints![0].X.Should().Be(100);
        _stage.LastPolygonPoints![0].Y.Should().Be(100);
        _stage.LastPolygonPoints![1].X.Should().Be(200);
        _stage.LastPolygonPoints![1].Y.Should().Be(100);
        _stage.LastPolygonPoints![2].X.Should().Be(200);
        _stage.LastPolygonPoints![2].Y.Should().Be(200);
        _stage.LastPolygonPoints![3].X.Should().Be(100);
        _stage.LastPolygonPoints![3].Y.Should().Be(200);
    }

    [Fact]
    public void Decode_DrawPolygon_WithContextStroke_PassesStroke()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };

        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 128, 64));
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer);

        _stage.LastPolygonStroke!.Value.R.Should().Be(255);
        _stage.LastPolygonStroke!.Value.G.Should().Be(128);
        _stage.LastPolygonStroke!.Value.B.Should().Be(64);
    }

    [Fact]
    public void Decode_DrawText_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawText(ops, "Hello World", 50, 100);
        });

        _decoder.Decode(buffer);

        _stage.DrawTextCalls.Should().Be(1);
        _stage.LastText.Should().Be("Hello World");
        _stage.LastTextX.Should().Be(50);
        _stage.LastTextY.Should().Be(100);
    }

    [Fact]
    public void Decode_DrawText_WithUtf8_HandlesSpecialChars()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawText(ops, "Caf\u00e9 \u2603", 0, 0);
        });

        _decoder.Decode(buffer);

        _stage.LastText.Should().Be("Caf\u00e9 \u2603");
    }

    [Fact]
    public void Decode_DrawCircle_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawCircle(ops, 150, 200, 50);
        });

        _decoder.Decode(buffer);

        _stage.DrawCircleCalls.Should().Be(1);
        _stage.LastCircleCenterX.Should().Be(150);
        _stage.LastCircleCenterY.Should().Be(200);
        _stage.LastCircleRadius.Should().Be(50);
    }

    [Fact]
    public void Decode_DrawRect_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawRect(ops, 10, 20, 100, 50);
        });

        _decoder.Decode(buffer);

        _stage.DrawRectCalls.Should().Be(1);
        _stage.LastRectX.Should().Be(10);
        _stage.LastRectY.Should().Be(20);
        _stage.LastRectWidth.Should().Be(100);
        _stage.LastRectHeight.Should().Be(50);
    }

    [Fact]
    public void Decode_DrawLine_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawLine(ops, 0, 0, 100, 100);
        });

        _decoder.Decode(buffer);

        _stage.DrawLineCalls.Should().Be(1);
        _stage.LastLineX1.Should().Be(0);
        _stage.LastLineY1.Should().Be(0);
        _stage.LastLineX2.Should().Be(100);
        _stage.LastLineY2.Should().Be(100);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_CompleteFrame_EncodesAndDecodesCorrectly()
    {
        var points = new SKPoint[] { new(10, 20), new(30, 40), new(50, 60) };
        var buffer = new byte[1024];
        int offset = 0;

        // Encode a complete frame
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer, 42, 2);

        // Layer 0: Master with context + polygon
        buffer[offset++] = 0; // layerId
        buffer[offset++] = (byte)FrameType.Master;
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), 3); // 3 ops
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(255, 0, 0));
        offset += VectorGraphicsEncoderV2.WriteSetThickness(buffer.AsSpan(offset), 3);
        offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.AsSpan(offset), points);

        // Layer 1: Remain
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 1);

        // End marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var result = _decoder.Decode(buffer.AsSpan(0, offset));

        result.Success.Should().BeTrue();
        result.FrameId.Should().Be(42UL);
        result.LayerCount.Should().Be(2);
        _stage.DrawPolygonCalls.Should().Be(1);
        _stage.LastPolygonPoints.Should().HaveCount(3);
        _stage.LastPolygonStroke!.Value.R.Should().Be(255);
        _stage.LastPolygonThickness.Should().Be(3);
    }

    [Fact]
    public void RoundTrip_NegativeCoordinates_PreservedCorrectly()
    {
        var points = new SKPoint[] { new(-100, -200), new(100, 200) };

        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawPolygon(ops, points);
        });

        _decoder.Decode(buffer);

        _stage.LastPolygonPoints![0].X.Should().Be(-100);
        _stage.LastPolygonPoints![0].Y.Should().Be(-200);
        _stage.LastPolygonPoints![1].X.Should().Be(100);
        _stage.LastPolygonPoints![1].Y.Should().Be(200);
    }

    [Fact]
    public void RoundTrip_LargePolygon_EncodesAndDecodesCorrectly()
    {
        var points = new SKPoint[100];
        for (int i = 0; i < 100; i++)
        {
            points[i] = new SKPoint(i * 10, i * 5);
        }

        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawPolygon(ops, points);
        });

        _decoder.Decode(buffer);

        _stage.LastPolygonPoints.Should().HaveCount(100);
        for (int i = 0; i < 100; i++)
        {
            _stage.LastPolygonPoints![i].X.Should().Be(i * 10);
            _stage.LastPolygonPoints![i].Y.Should().Be(i * 5);
        }
    }

    #endregion

    #region LayerContext Tests

    [Fact]
    public void LayerContext_GetTransformMatrix_ReturnsIdentityByDefault()
    {
        var context = new LayerContext();

        var matrix = context.GetTransformMatrix();

        matrix.Should().Be(SKMatrix.Identity);
    }

    [Fact]
    public void LayerContext_GetTransformMatrix_AppliesOffset()
    {
        var context = new LayerContext { Offset = new SKPoint(100, 200) };

        var matrix = context.GetTransformMatrix();

        matrix.TransX.Should().Be(100);
        matrix.TransY.Should().Be(200);
    }

    [Fact]
    public void LayerContext_GetTransformMatrix_AppliesScale()
    {
        var context = new LayerContext { Scale = new SKPoint(2, 3) };

        var matrix = context.GetTransformMatrix();

        matrix.ScaleX.Should().Be(2);
        matrix.ScaleY.Should().Be(3);
    }

    [Fact]
    public void LayerContext_GetTransformMatrix_ReturnsExplicitMatrixWhenSet()
    {
        var explicitMatrix = new SKMatrix(1.5f, 0.1f, 50f, 0.2f, 2.0f, 100f, 0, 0, 1);
        var context = new LayerContext
        {
            Matrix = explicitMatrix,
            Scale = new SKPoint(5, 5) // Should be ignored when Matrix is set
        };

        var matrix = context.GetTransformMatrix();

        matrix.Should().Be(explicitMatrix);
    }

    [Fact]
    public void LayerContext_Reset_RestoresDefaults()
    {
        var context = new LayerContext
        {
            Stroke = new RgbColor(255, 0, 0),
            Thickness = 5,
            Rotation = 45
        };

        context.Reset();

        context.Stroke.Should().Be(RgbColor.Black);
        context.Thickness.Should().Be(1);
        context.Rotation.Should().Be(0);
    }

    #endregion

    #region Helper Methods

    private static byte[] CreateMinimalMessage(ulong frameId, byte layerCount, byte layerId, FrameType frameType)
    {
        var buffer = new byte[256];
        int offset = 0;

        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer, frameId, layerCount);

        buffer[offset++] = layerId;
        buffer[offset++] = (byte)frameType;

        if (frameType == FrameType.Master)
        {
            // Op count = 0
            offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), 0);
        }

        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        return buffer.AsSpan(0, offset).ToArray();
    }

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

    #endregion
}

/// <summary>
/// Test implementation of IStage that captures all callbacks for verification.
/// </summary>
internal class TestStage : IStage
{
    private readonly Dictionary<byte, TestCanvas> _canvases = new();

    public int FrameStartCalls { get; private set; }
    public int FrameEndCalls { get; private set; }
    public int ClearLayerCalls { get; private set; }

    public ulong LastFrameId { get; private set; }
    public byte LastClearedLayerId { get; private set; }

    // Aggregate properties from canvases
    public int DrawPolygonCalls => _canvases.Values.Sum(c => c.DrawPolygonCalls);
    public int DrawTextCalls => _canvases.Values.Sum(c => c.DrawTextCalls);
    public int DrawCircleCalls => _canvases.Values.Sum(c => c.DrawCircleCalls);
    public int DrawRectCalls => _canvases.Values.Sum(c => c.DrawRectCalls);
    public int DrawLineCalls => _canvases.Values.Sum(c => c.DrawLineCalls);
    public int SaveCalls => _canvases.Values.Sum(c => c.SaveCalls);
    public int RestoreCalls => _canvases.Values.Sum(c => c.RestoreCalls);
    public int SetMatrixCalls => _canvases.Values.Sum(c => c.SetMatrixCalls);

    // Last values from any canvas
    public SKPoint[]? LastPolygonPoints => _canvases.Values.LastOrDefault(c => c.LastPolygonPoints != null)?.LastPolygonPoints;
    public RgbColor? LastPolygonStroke => _canvases.Values.LastOrDefault(c => c.LastPolygonStroke.HasValue)?.LastPolygonStroke;
    public int? LastPolygonThickness => _canvases.Values.LastOrDefault(c => c.LastPolygonThickness.HasValue)?.LastPolygonThickness;
    public string? LastText => _canvases.Values.LastOrDefault(c => c.LastText != null)?.LastText;
    public int LastTextX => _canvases.Values.LastOrDefault(c => c.LastText != null)?.LastTextX ?? 0;
    public int LastTextY => _canvases.Values.LastOrDefault(c => c.LastText != null)?.LastTextY ?? 0;
    public int LastCircleCenterX => _canvases.Values.LastOrDefault(c => c.DrawCircleCalls > 0)?.LastCircleCenterX ?? 0;
    public int LastCircleCenterY => _canvases.Values.LastOrDefault(c => c.DrawCircleCalls > 0)?.LastCircleCenterY ?? 0;
    public int LastCircleRadius => _canvases.Values.LastOrDefault(c => c.DrawCircleCalls > 0)?.LastCircleRadius ?? 0;
    public int LastRectX => _canvases.Values.LastOrDefault(c => c.DrawRectCalls > 0)?.LastRectX ?? 0;
    public int LastRectY => _canvases.Values.LastOrDefault(c => c.DrawRectCalls > 0)?.LastRectY ?? 0;
    public int LastRectWidth => _canvases.Values.LastOrDefault(c => c.DrawRectCalls > 0)?.LastRectWidth ?? 0;
    public int LastRectHeight => _canvases.Values.LastOrDefault(c => c.DrawRectCalls > 0)?.LastRectHeight ?? 0;
    public int LastLineX1 => _canvases.Values.LastOrDefault(c => c.DrawLineCalls > 0)?.LastLineX1 ?? 0;
    public int LastLineY1 => _canvases.Values.LastOrDefault(c => c.DrawLineCalls > 0)?.LastLineY1 ?? 0;
    public int LastLineX2 => _canvases.Values.LastOrDefault(c => c.DrawLineCalls > 0)?.LastLineX2 ?? 0;
    public int LastLineY2 => _canvases.Values.LastOrDefault(c => c.DrawLineCalls > 0)?.LastLineY2 ?? 0;
    public SKMatrix LastMatrix => _canvases.Values.LastOrDefault(c => c.SetMatrixCalls > 0)?.LastMatrix ?? SKMatrix.Identity;

    public ICanvas this[byte layerId]
    {
        get
        {
            if (!_canvases.TryGetValue(layerId, out var canvas))
            {
                canvas = new TestCanvas();
                _canvases[layerId] = canvas;
            }
            return canvas;
        }
    }

    public TestCanvas GetCanvas(byte layerId) => (TestCanvas)this[layerId];

    public void OnFrameStart(ulong frameId)
    {
        FrameStartCalls++;
        LastFrameId = frameId;
    }

    public void OnFrameEnd()
    {
        FrameEndCalls++;
    }

    public void Clear(byte layerId)
    {
        ClearLayerCalls++;
        LastClearedLayerId = layerId;
    }

    public void Remain(byte layerId)
    {
        // Test stub - capture that Remain was called
    }

    public bool TryCopyFrame(out global::BlazorBlaze.ValueTypes.RefArray<global::BlazorBlaze.ValueTypes.Lease<global::BlazorBlaze.VectorGraphics.Protocol.ILayer>>? copy)
    {
        // Test stub - not needed for decoder tests
        copy = null;
        return false;
    }
}

/// <summary>
/// Test implementation of ICanvas that captures all calls for verification.
/// </summary>
internal class TestCanvas : ICanvas
{
    public int SaveCalls { get; private set; }
    public int RestoreCalls { get; private set; }
    public int SetMatrixCalls { get; private set; }
    public int DrawPolygonCalls { get; private set; }
    public int DrawTextCalls { get; private set; }
    public int DrawCircleCalls { get; private set; }
    public int DrawRectCalls { get; private set; }
    public int DrawLineCalls { get; private set; }

    public SKMatrix LastMatrix { get; private set; }
    public SKPoint[]? LastPolygonPoints { get; private set; }
    public RgbColor? LastPolygonStroke { get; private set; }
    public int? LastPolygonThickness { get; private set; }

    public string? LastText { get; private set; }
    public int LastTextX { get; private set; }
    public int LastTextY { get; private set; }
    public RgbColor? LastTextColor { get; private set; }
    public int? LastTextFontSize { get; private set; }

    public int LastCircleCenterX { get; private set; }
    public int LastCircleCenterY { get; private set; }
    public int LastCircleRadius { get; private set; }

    public int LastRectX { get; private set; }
    public int LastRectY { get; private set; }
    public int LastRectWidth { get; private set; }
    public int LastRectHeight { get; private set; }

    public int LastLineX1 { get; private set; }
    public int LastLineY1 { get; private set; }
    public int LastLineX2 { get; private set; }
    public int LastLineY2 { get; private set; }

    public void Save() => SaveCalls++;

    public void Restore() => RestoreCalls++;

    public void SetMatrix(SKMatrix matrix)
    {
        SetMatrixCalls++;
        LastMatrix = matrix;
    }

    public void DrawPolygon(ReadOnlySpan<SKPoint> points, RgbColor stroke, int thickness)
    {
        DrawPolygonCalls++;
        LastPolygonPoints = points.ToArray();
        LastPolygonStroke = stroke;
        LastPolygonThickness = thickness;
    }

    public void DrawText(string text, int x, int y, RgbColor color, int fontSize)
    {
        DrawTextCalls++;
        LastText = text;
        LastTextX = x;
        LastTextY = y;
        LastTextColor = color;
        LastTextFontSize = fontSize;
    }

    public void DrawCircle(int centerX, int centerY, int radius, RgbColor stroke, int thickness)
    {
        DrawCircleCalls++;
        LastCircleCenterX = centerX;
        LastCircleCenterY = centerY;
        LastCircleRadius = radius;
    }

    public void DrawRect(int x, int y, int width, int height, RgbColor stroke, int thickness)
    {
        DrawRectCalls++;
        LastRectX = x;
        LastRectY = y;
        LastRectWidth = width;
        LastRectHeight = height;
    }

    public void DrawLine(int x1, int y1, int x2, int y2, RgbColor stroke, int thickness)
    {
        DrawLineCalls++;
        LastLineX1 = x1;
        LastLineY1 = y1;
        LastLineX2 = x2;
        LastLineY2 = y2;
    }
}
