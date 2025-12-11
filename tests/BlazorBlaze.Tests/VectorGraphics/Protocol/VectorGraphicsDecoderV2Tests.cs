using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using NSubstitute;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.Tests.VectorGraphics.Protocol;

public class VectorGraphicsDecoderV2Tests
{
    private readonly VectorGraphicsDecoderV2 _decoder;
    private readonly TestDecoderCallback _callback;

    public VectorGraphicsDecoderV2Tests()
    {
        _decoder = new VectorGraphicsDecoderV2();
        _callback = new TestDecoderCallback();
    }

    #region Message Header Tests

    [Fact]
    public void Decode_InsufficientData_ReturnsNeedMoreData()
    {
        var smallBuffer = new byte[] { 0x00, 0x01, 0x02 };

        var result = _decoder.Decode(smallBuffer, _callback);

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

        var result = _decoder.Decode(buffer.AsSpan(0, offset), _callback);

        result.Success.Should().BeTrue();
        result.FrameId.Should().Be(12345UL);
        result.LayerCount.Should().Be(1);
        result.BytesConsumed.Should().Be(offset);
    }

    [Fact]
    public void Decode_CallsFrameStartAndEnd()
    {
        var buffer = CreateMinimalMessage(frameId: 100, layerCount: 1, layerId: 5, FrameType.Remain);

        _decoder.Decode(buffer, _callback);

        _callback.FrameStartCalls.Should().Be(1);
        _callback.LastFrameId.Should().Be(100UL);
        _callback.LastLayerCount.Should().Be(1);
        _callback.FrameEndCalls.Should().Be(1);
    }

    #endregion

    #region Layer Block Tests

    [Fact]
    public void Decode_LayerRemain_CallsLayerStartEnd()
    {
        var buffer = CreateMinimalMessage(frameId: 1, layerCount: 1, layerId: 3, FrameType.Remain);

        _decoder.Decode(buffer, _callback);

        _callback.LayerStartCalls.Should().Be(1);
        _callback.LastLayerId.Should().Be(3);
        _callback.LastFrameType.Should().Be(FrameType.Remain);
        _callback.LayerEndCalls.Should().Be(1);
    }

    [Fact]
    public void Decode_LayerClear_CallsLayerStartEnd()
    {
        var buffer = CreateMinimalMessage(frameId: 1, layerCount: 1, layerId: 7, FrameType.Clear);

        _decoder.Decode(buffer, _callback);

        _callback.LastFrameType.Should().Be(FrameType.Clear);
    }

    [Fact]
    public void Decode_MultipleLayers_ProcessesAllLayers()
    {
        var buffer = new byte[256];
        int offset = 0;

        // Header: 3 layers
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer, 1, 3);
        // Layer 0: Remain
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 0);
        // Layer 1: Clear
        offset += VectorGraphicsEncoderV2.WriteLayerClear(buffer.AsSpan(offset), 1);
        // Layer 2: Remain
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 2);
        // End marker
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        var result = _decoder.Decode(buffer.AsSpan(0, offset), _callback);

        result.Success.Should().BeTrue();
        result.LayerCount.Should().Be(3);
        _callback.LayerStartCalls.Should().Be(3);
        _callback.LayerEndCalls.Should().Be(3);
    }

    #endregion

    #region Context Operations Tests

    [Fact]
    public void Decode_SetStroke_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 128, 64, 200));
        });

        _decoder.Decode(buffer, _callback);

        _callback.SetContextCalls.Should().Be(1);
        _callback.LastContext!.Stroke.R.Should().Be(255);
        _callback.LastContext!.Stroke.G.Should().Be(128);
        _callback.LastContext!.Stroke.B.Should().Be(64);
        _callback.LastContext!.Stroke.A.Should().Be(200);
    }

    [Fact]
    public void Decode_SetFill_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetFill(ops, new RgbColor(100, 150, 200, 128));
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.Fill.R.Should().Be(100);
        _callback.LastContext!.Fill.G.Should().Be(150);
        _callback.LastContext!.Fill.B.Should().Be(200);
        _callback.LastContext!.Fill.A.Should().Be(128);
    }

    [Fact]
    public void Decode_SetThickness_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetThickness(ops, 5);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.Thickness.Should().Be(5);
    }

    [Fact]
    public void Decode_SetFontSize_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetFontSize(ops, 24);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.FontSize.Should().Be(24);
    }

    [Fact]
    public void Decode_SetFontColor_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetFontColor(ops, new RgbColor(10, 20, 30, 255));
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.FontColor.R.Should().Be(10);
        _callback.LastContext!.FontColor.G.Should().Be(20);
        _callback.LastContext!.FontColor.B.Should().Be(30);
    }

    [Fact]
    public void Decode_SetOffset_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetOffset(ops, 100, 200);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.Offset.X.Should().Be(100);
        _callback.LastContext!.Offset.Y.Should().Be(200);
    }

    [Fact]
    public void Decode_SetRotation_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetRotation(ops, 45.0f);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.Rotation.Should().Be(45.0f);
    }

    [Fact]
    public void Decode_SetScale_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetScale(ops, 2.0f, 1.5f);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.Scale.X.Should().Be(2.0f);
        _callback.LastContext!.Scale.Y.Should().Be(1.5f);
    }

    [Fact]
    public void Decode_SetSkew_UpdatesContext()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetSkew(ops, 0.5f, 0.25f);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.Skew.X.Should().Be(0.5f);
        _callback.LastContext!.Skew.Y.Should().Be(0.25f);
    }

    [Fact]
    public void Decode_SetMatrix_UpdatesContext()
    {
        var matrix = new SKMatrix(1.5f, 0.1f, 100f, 0.2f, 2.0f, 200f, 0, 0, 1);
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSetMatrix(ops, matrix);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastContext!.Matrix.Should().NotBeNull();
        _callback.LastContext!.Matrix!.Value.ScaleX.Should().Be(1.5f);
        _callback.LastContext!.Matrix!.Value.SkewX.Should().Be(0.1f);
        _callback.LastContext!.Matrix!.Value.TransX.Should().Be(100f);
        _callback.LastContext!.Matrix!.Value.SkewY.Should().Be(0.2f);
        _callback.LastContext!.Matrix!.Value.ScaleY.Should().Be(2.0f);
        _callback.LastContext!.Matrix!.Value.TransY.Should().Be(200f);
    }

    [Fact]
    public void Decode_SaveContext_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteSaveContext(ops);
        });

        _decoder.Decode(buffer, _callback);

        _callback.SaveContextCalls.Should().Be(1);
    }

    [Fact]
    public void Decode_RestoreContext_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSaveContext(ops);
            offset += VectorGraphicsEncoderV2.WriteRestoreContext(ops.Slice(offset));
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer, _callback);

        _callback.RestoreContextCalls.Should().Be(1);
    }

    [Fact]
    public void Decode_ResetContext_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteResetContext(ops);
        });

        _decoder.Decode(buffer, _callback);

        _callback.ResetContextCalls.Should().Be(1);
    }

    [Fact]
    public void Decode_SaveAndRestore_PreservesContext()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };

        // Test that context is properly saved and restored by checking what is passed to draw calls
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 0, 0)); // Red
            offset += VectorGraphicsEncoderV2.WriteSaveContext(ops.Slice(offset));
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops.Slice(offset), new RgbColor(0, 255, 0)); // Green
            offset += VectorGraphicsEncoderV2.WriteRestoreContext(ops.Slice(offset));
            // Now draw a polygon - it should use the restored red context
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 5);

        _decoder.Decode(buffer, _callback);

        // After restore + draw, the polygon context should have the restored red stroke
        _callback.LastPolygonContext!.Stroke.R.Should().Be(255);
        _callback.LastPolygonContext!.Stroke.G.Should().Be(0);
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

        _decoder.Decode(buffer, _callback);

        _callback.DrawPolygonCalls.Should().Be(1);
        _callback.LastPolygonPoints.Should().HaveCount(4);
        _callback.LastPolygonPoints![0].X.Should().Be(100);
        _callback.LastPolygonPoints![0].Y.Should().Be(100);
        _callback.LastPolygonPoints![1].X.Should().Be(200);
        _callback.LastPolygonPoints![1].Y.Should().Be(100);
        _callback.LastPolygonPoints![2].X.Should().Be(200);
        _callback.LastPolygonPoints![2].Y.Should().Be(200);
        _callback.LastPolygonPoints![3].X.Should().Be(100);
        _callback.LastPolygonPoints![3].Y.Should().Be(200);
    }

    [Fact]
    public void Decode_DrawPolygon_WithContextStroke_PassesContext()
    {
        var points = new SKPoint[] { new(0, 0), new(100, 100) };

        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            int offset = 0;
            offset += VectorGraphicsEncoderV2.WriteSetStroke(ops, new RgbColor(255, 128, 64));
            offset += VectorGraphicsEncoderV2.WriteDrawPolygon(ops.Slice(offset), points);
            return offset;
        }, opCount: 2);

        _decoder.Decode(buffer, _callback);

        _callback.LastPolygonContext!.Stroke.R.Should().Be(255);
        _callback.LastPolygonContext!.Stroke.G.Should().Be(128);
        _callback.LastPolygonContext!.Stroke.B.Should().Be(64);
    }

    [Fact]
    public void Decode_DrawText_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawText(ops, "Hello World", 50, 100);
        });

        _decoder.Decode(buffer, _callback);

        _callback.DrawTextCalls.Should().Be(1);
        _callback.LastText.Should().Be("Hello World");
        _callback.LastTextX.Should().Be(50);
        _callback.LastTextY.Should().Be(100);
    }

    [Fact]
    public void Decode_DrawText_WithUtf8_HandlesSpecialChars()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawText(ops, "Caf\u00e9 \u2603", 0, 0);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastText.Should().Be("Caf\u00e9 \u2603");
    }

    [Fact]
    public void Decode_DrawCircle_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawCircle(ops, 150, 200, 50);
        });

        _decoder.Decode(buffer, _callback);

        _callback.DrawCircleCalls.Should().Be(1);
        _callback.LastCircleCenterX.Should().Be(150);
        _callback.LastCircleCenterY.Should().Be(200);
        _callback.LastCircleRadius.Should().Be(50);
    }

    [Fact]
    public void Decode_DrawRect_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawRect(ops, 10, 20, 100, 50);
        });

        _decoder.Decode(buffer, _callback);

        _callback.DrawRectCalls.Should().Be(1);
        _callback.LastRectX.Should().Be(10);
        _callback.LastRectY.Should().Be(20);
        _callback.LastRectWidth.Should().Be(100);
        _callback.LastRectHeight.Should().Be(50);
    }

    [Fact]
    public void Decode_DrawLine_CallsCallback()
    {
        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawLine(ops, 0, 0, 100, 100);
        });

        _decoder.Decode(buffer, _callback);

        _callback.DrawLineCalls.Should().Be(1);
        _callback.LastLineX1.Should().Be(0);
        _callback.LastLineY1.Should().Be(0);
        _callback.LastLineX2.Should().Be(100);
        _callback.LastLineY2.Should().Be(100);
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
        var result = _decoder.Decode(buffer.AsSpan(0, offset), _callback);

        result.Success.Should().BeTrue();
        result.FrameId.Should().Be(42UL);
        result.LayerCount.Should().Be(2);
        _callback.DrawPolygonCalls.Should().Be(1);
        _callback.LastPolygonPoints.Should().HaveCount(3);
        _callback.LastPolygonContext!.Stroke.R.Should().Be(255);
        _callback.LastPolygonContext!.Thickness.Should().Be(3);
    }

    [Fact]
    public void RoundTrip_NegativeCoordinates_PreservedCorrectly()
    {
        var points = new SKPoint[] { new(-100, -200), new(100, 200) };

        var buffer = CreateMessageWithOps(frameId: 1, layerId: 0, ops =>
        {
            return VectorGraphicsEncoderV2.WriteDrawPolygon(ops, points);
        });

        _decoder.Decode(buffer, _callback);

        _callback.LastPolygonPoints![0].X.Should().Be(-100);
        _callback.LastPolygonPoints![0].Y.Should().Be(-200);
        _callback.LastPolygonPoints![1].X.Should().Be(100);
        _callback.LastPolygonPoints![1].Y.Should().Be(200);
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

        _decoder.Decode(buffer, _callback);

        _callback.LastPolygonPoints.Should().HaveCount(100);
        for (int i = 0; i < 100; i++)
        {
            _callback.LastPolygonPoints![i].X.Should().Be(i * 10);
            _callback.LastPolygonPoints![i].Y.Should().Be(i * 5);
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
/// Test implementation of IDecoderCallbackV2 that captures all callbacks for verification.
/// </summary>
internal class TestDecoderCallback : IDecoderCallbackV2
{
    public int FrameStartCalls { get; private set; }
    public int FrameEndCalls { get; private set; }
    public int LayerStartCalls { get; private set; }
    public int LayerEndCalls { get; private set; }
    public int SetContextCalls { get; private set; }
    public int SaveContextCalls { get; private set; }
    public int RestoreContextCalls { get; private set; }
    public int ResetContextCalls { get; private set; }
    public int DrawPolygonCalls { get; private set; }
    public int DrawTextCalls { get; private set; }
    public int DrawCircleCalls { get; private set; }
    public int DrawRectCalls { get; private set; }
    public int DrawLineCalls { get; private set; }

    public ulong LastFrameId { get; private set; }
    public byte LastLayerCount { get; private set; }
    public byte LastLayerId { get; private set; }
    public FrameType LastFrameType { get; private set; }

    public LayerContext? LastContext { get; private set; }
    public SKPoint[]? LastPolygonPoints { get; private set; }
    public LayerContext? LastPolygonContext { get; private set; }

    public string? LastText { get; private set; }
    public int LastTextX { get; private set; }
    public int LastTextY { get; private set; }

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

    public void OnFrameStart(ulong frameId, byte layerCount)
    {
        FrameStartCalls++;
        LastFrameId = frameId;
        LastLayerCount = layerCount;
    }

    public void OnLayerStart(byte layerId, FrameType frameType)
    {
        LayerStartCalls++;
        LastLayerId = layerId;
        LastFrameType = frameType;
    }

    public void OnLayerEnd(byte layerId)
    {
        LayerEndCalls++;
    }

    public void OnFrameEnd()
    {
        FrameEndCalls++;
    }

    public void OnSetContext(byte layerId, LayerContext context)
    {
        SetContextCalls++;
        LastContext = CloneContext(context);
    }

    public void OnSaveContext(byte layerId)
    {
        SaveContextCalls++;
    }

    public void OnRestoreContext(byte layerId)
    {
        RestoreContextCalls++;
    }

    public void OnResetContext(byte layerId)
    {
        ResetContextCalls++;
    }

    public void OnDrawPolygon(byte layerId, ReadOnlySpan<SKPoint> points, LayerContext context)
    {
        DrawPolygonCalls++;
        LastPolygonPoints = points.ToArray();
        LastPolygonContext = CloneContext(context);
    }

    public void OnDrawText(byte layerId, string text, int x, int y, LayerContext context)
    {
        DrawTextCalls++;
        LastText = text;
        LastTextX = x;
        LastTextY = y;
    }

    public void OnDrawCircle(byte layerId, int centerX, int centerY, int radius, LayerContext context)
    {
        DrawCircleCalls++;
        LastCircleCenterX = centerX;
        LastCircleCenterY = centerY;
        LastCircleRadius = radius;
    }

    public void OnDrawRect(byte layerId, int x, int y, int width, int height, LayerContext context)
    {
        DrawRectCalls++;
        LastRectX = x;
        LastRectY = y;
        LastRectWidth = width;
        LastRectHeight = height;
    }

    public void OnDrawLine(byte layerId, int x1, int y1, int x2, int y2, LayerContext context)
    {
        DrawLineCalls++;
        LastLineX1 = x1;
        LastLineY1 = y1;
        LastLineX2 = x2;
        LastLineY2 = y2;
    }

    private static LayerContext CloneContext(LayerContext ctx) => new()
    {
        Stroke = ctx.Stroke,
        Fill = ctx.Fill,
        Thickness = ctx.Thickness,
        FontSize = ctx.FontSize,
        FontColor = ctx.FontColor,
        Offset = ctx.Offset,
        Rotation = ctx.Rotation,
        Scale = ctx.Scale,
        Skew = ctx.Skew,
        Matrix = ctx.Matrix
    };
}
