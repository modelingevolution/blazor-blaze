using System.Collections.Concurrent;
using BlazorBlaze.ValueTypes;
using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using SkiaSharp;

namespace BlazorBlaze.Tests.VectorGraphics.Protocol;

/// <summary>
/// Integration tests that verify encoder output can be correctly decoded.
/// Tests all draw types, context operations, and layer composition scenarios.
/// </summary>
public class EncoderDecoderIntegrationTests
{
    /// <summary>
    /// Test canvas that records all operations for verification.
    /// </summary>
    private class RecordingCanvas : ICanvas
    {
        public List<string> Operations { get; } = new();
        public RgbColor LastStroke { get; private set; }
        public RgbColor LastFill { get; private set; }
        public int LastThickness { get; private set; }
        public SKMatrix LastMatrix { get; private set; } = SKMatrix.Identity;
        public int SaveCount { get; private set; }
        public int RestoreCount { get; private set; }

        // Track draw operations with parameters
        public List<(string Type, object[] Params)> DrawCalls { get; } = new();

        public void Save()
        {
            SaveCount++;
            Operations.Add("Save");
        }

        public void Restore()
        {
            RestoreCount++;
            Operations.Add("Restore");
        }

        public void SetMatrix(SKMatrix matrix)
        {
            LastMatrix = matrix;
            Operations.Add($"SetMatrix({matrix.ScaleX:F2},{matrix.SkewX:F2},{matrix.TransX:F2},{matrix.SkewY:F2},{matrix.ScaleY:F2},{matrix.TransY:F2})");
        }

        public void DrawPolygon(ReadOnlySpan<SKPoint> points, RgbColor stroke, int thickness)
        {
            var pointsCopy = points.ToArray();
            DrawCalls.Add(("DrawPolygon", new object[] { pointsCopy, stroke, thickness }));
            Operations.Add($"DrawPolygon(points={points.Length}, stroke={stroke}, thickness={thickness})");
        }

        public void DrawText(string text, int x, int y, RgbColor color, int fontSize)
        {
            DrawCalls.Add(("DrawText", new object[] { text, x, y, color, fontSize }));
            Operations.Add($"DrawText(\"{text}\", {x}, {y}, color={color}, fontSize={fontSize})");
        }

        public void DrawCircle(int centerX, int centerY, int radius, RgbColor stroke, int thickness)
        {
            DrawCalls.Add(("DrawCircle", new object[] { centerX, centerY, radius, stroke, thickness }));
            Operations.Add($"DrawCircle({centerX}, {centerY}, r={radius}, stroke={stroke}, thickness={thickness})");
        }

        public void DrawRect(int x, int y, int width, int height, RgbColor stroke, int thickness)
        {
            DrawCalls.Add(("DrawRect", new object[] { x, y, width, height, stroke, thickness }));
            Operations.Add($"DrawRect({x}, {y}, {width}x{height}, stroke={stroke}, thickness={thickness})");
        }

        public void DrawLine(int x1, int y1, int x2, int y2, RgbColor stroke, int thickness)
        {
            DrawCalls.Add(("DrawLine", new object[] { x1, y1, x2, y2, stroke, thickness }));
            Operations.Add($"DrawLine({x1},{y1} -> {x2},{y2}, stroke={stroke}, thickness={thickness})");
        }

        public void DrawJpeg(in ReadOnlySpan<byte> jpegData, int x, int y, int width, int height)
        {
            DrawCalls.Add(("DrawJpeg", new object[] { jpegData.ToArray(), x, y, width, height }));
            Operations.Add($"DrawJpeg({jpegData.Length} bytes, {x}, {y}, {width}x{height})");
        }

        public void Clear() => Operations.Add("Clear");
        public void DrawTo(SKCanvas canvas) { }
        public void Dispose() { }
    }

    /// <summary>
    /// Test stage that uses recording canvases.
    /// </summary>
    private class RecordingStage : IStage
    {
        private readonly Dictionary<byte, RecordingCanvas> _canvases = new();
        private readonly List<byte> _clearedLayers = new();
        private readonly List<byte> _remainedLayers = new();

        public ulong CurrentFrameId { get; private set; }
        public List<byte> ClearedLayers => _clearedLayers;
        public List<byte> RemainedLayers => _remainedLayers;

        public RecordingCanvas GetCanvas(byte layerId) =>
            _canvases.TryGetValue(layerId, out var c) ? c : throw new InvalidOperationException($"Layer {layerId} not found");

        public ICanvas this[byte layerId] => _canvases[layerId];

        public void OnFrameStart(ulong frameId)
        {
            CurrentFrameId = frameId;
            _clearedLayers.Clear();
            _remainedLayers.Clear();
        }

        public void Clear(byte layerId)
        {
            _canvases[layerId] = new RecordingCanvas();
            _clearedLayers.Add(layerId);
        }

        public void Remain(byte layerId)
        {
            // For Remain, keep existing canvas or create if first frame
            if (!_canvases.ContainsKey(layerId))
                throw new InvalidOperationException($"Remain failed for layer {layerId}");
            _remainedLayers.Add(layerId);
        }

        public void OnFrameEnd() { }

        public bool TryCopyFrame(out RefArray<Lease<ILayer>>? copy)
        {
            copy = null;
            return false; // Not needed for these tests
        }
    }

    #region Helper Methods

    private static byte[] EncodeFrame(Action<byte[], int> encodeAction)
    {
        var buffer = new byte[8192];
        var wrapper = new { Offset = 0 };

        // We need a mutable reference
        int offset = 0;
        encodeAction(buffer, offset);

        // Return only the used portion
        return buffer;
    }

    #endregion

    #region Single Draw Operation Round-Trip Tests

    [Fact]
    public void RoundTrip_DrawCircle_CorrectValues()
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Encode
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 2);
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(255, 0, 0));
        offset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.AsSpan(offset), 100, 200, 50);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();
        result.FrameId.Should().Be(1);

        var canvas = stage.GetCanvas(0);
        canvas.DrawCalls.Should().HaveCount(1);
        canvas.DrawCalls[0].Type.Should().Be("DrawCircle");
        canvas.DrawCalls[0].Params[0].Should().Be(100); // centerX
        canvas.DrawCalls[0].Params[1].Should().Be(200); // centerY
        canvas.DrawCalls[0].Params[2].Should().Be(50);  // radius
    }

    [Fact]
    public void RoundTrip_DrawRect_CorrectValues()
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Encode
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 2);
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(0, 255, 0));
        offset += VectorGraphicsEncoderV2.WriteDrawRect(buffer.AsSpan(offset), 10, 20, 300, 150);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        canvas.DrawCalls.Should().HaveCount(1);
        canvas.DrawCalls[0].Type.Should().Be("DrawRect");
        canvas.DrawCalls[0].Params[0].Should().Be(10);  // x
        canvas.DrawCalls[0].Params[1].Should().Be(20);  // y
        canvas.DrawCalls[0].Params[2].Should().Be(300); // width
        canvas.DrawCalls[0].Params[3].Should().Be(150); // height
    }

    [Fact]
    public void RoundTrip_DrawLine_CorrectValues()
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Encode
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 2);
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(0, 0, 255));
        offset += VectorGraphicsEncoderV2.WriteDrawLine(buffer.AsSpan(offset), 0, 0, 500, 400);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        canvas.DrawCalls.Should().HaveCount(1);
        canvas.DrawCalls[0].Type.Should().Be("DrawLine");
        canvas.DrawCalls[0].Params[0].Should().Be(0);   // x1
        canvas.DrawCalls[0].Params[1].Should().Be(0);   // y1
        canvas.DrawCalls[0].Params[2].Should().Be(500); // x2
        canvas.DrawCalls[0].Params[3].Should().Be(400); // y2
    }

    [Fact]
    public void RoundTrip_DrawPolygon_CorrectPoints()
    {
        var buffer = new byte[1024];
        int offset = 0;

        var originalPoints = new SKPoint[]
        {
            new(100, 100),
            new(200, 100),
            new(200, 200),
            new(150, 250),
            new(100, 200)
        };

        // Encode
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 2);
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(255, 255, 0));
        offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.AsSpan(offset), originalPoints);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        canvas.DrawCalls.Should().HaveCount(1);
        canvas.DrawCalls[0].Type.Should().Be("DrawPolygon");

        var decodedPoints = (SKPoint[])canvas.DrawCalls[0].Params[0];
        decodedPoints.Should().HaveCount(originalPoints.Length);

        for (int i = 0; i < originalPoints.Length; i++)
        {
            decodedPoints[i].X.Should().Be(originalPoints[i].X, $"Point {i} X mismatch");
            decodedPoints[i].Y.Should().Be(originalPoints[i].Y, $"Point {i} Y mismatch");
        }
    }

    [Fact]
    public void RoundTrip_DrawText_CorrectValues()
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Encode
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 3);
        offset += VectorGraphicsEncoderV2.WriteSetFontSize(buffer.AsSpan(offset), 24);
        offset += VectorGraphicsEncoderV2.WriteSetFontColor(buffer.AsSpan(offset), new RgbColor(255, 255, 255));
        offset += VectorGraphicsEncoderV2.WriteDrawText(buffer.AsSpan(offset), "Hello World!", 50, 100);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        canvas.DrawCalls.Should().HaveCount(1);
        canvas.DrawCalls[0].Type.Should().Be("DrawText");
        canvas.DrawCalls[0].Params[0].Should().Be("Hello World!"); // text
        canvas.DrawCalls[0].Params[1].Should().Be(50);  // x
        canvas.DrawCalls[0].Params[2].Should().Be(100); // y
    }

    [Fact]
    public void RoundTrip_DrawText_UnicodeCharacters()
    {
        var buffer = new byte[1024];
        int offset = 0;

        string unicodeText = "Héllo 世界 🚀";

        // Encode
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 2);
        offset += VectorGraphicsEncoderV2.WriteSetFontSize(buffer.AsSpan(offset), 16);
        offset += VectorGraphicsEncoderV2.WriteDrawText(buffer.AsSpan(offset), unicodeText, 10, 20);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        canvas.DrawCalls[0].Params[0].Should().Be(unicodeText);
    }

    #endregion

    #region Context and Transform Round-Trip Tests

    [Fact]
    public void RoundTrip_SetStroke_CorrectColor()
    {
        var buffer = new byte[1024];
        int offset = 0;

        var color = new RgbColor(128, 64, 32, 200);

        // Encode
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 2);
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), color);
        offset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.AsSpan(offset), 100, 100, 50);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify stroke color was passed to draw call
        result.Success.Should().BeTrue();
        var canvas = stage.GetCanvas(0);
        var drawStroke = (RgbColor)canvas.DrawCalls[0].Params[3]; // DrawCircle stroke param
        drawStroke.R.Should().Be(128);
        drawStroke.G.Should().Be(64);
        drawStroke.B.Should().Be(32);
        drawStroke.A.Should().Be(200);
    }

    [Fact]
    public void RoundTrip_SaveRestore_CorrectOrder()
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Encode: Save, modify, draw, Restore
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 5);
        offset += VectorGraphicsEncoderV2.WriteSaveContext(buffer.AsSpan(offset));
        offset += VectorGraphicsEncoderV2.WriteSetOffset(buffer.AsSpan(offset), 100, 100);
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), RgbColor.Red);
        offset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.AsSpan(offset), 0, 0, 25);
        offset += VectorGraphicsEncoderV2.WriteRestoreContext(buffer.AsSpan(offset));
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        canvas.SaveCount.Should().Be(1);
        canvas.RestoreCount.Should().Be(1);

        // Operations should be in order
        canvas.Operations.Should().ContainInConsecutiveOrder("Save", "SetMatrix");
        canvas.Operations.Should().Contain(op => op.StartsWith("DrawCircle"));
        canvas.Operations.Last().Should().Be("Restore");
    }

    [Fact]
    public void RoundTrip_Transforms_CorrectMatrix()
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Encode translate + rotate
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 3);
        offset += VectorGraphicsEncoderV2.WriteSetOffset(buffer.AsSpan(offset), 200, 150);
        offset += VectorGraphicsEncoderV2.WriteSetRotation(buffer.AsSpan(offset), 45);
        offset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.AsSpan(offset), 0, 0, 50);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify transforms were applied
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        // The matrix should have translation (200, 150) and 45 degree rotation
        canvas.LastMatrix.TransX.Should().BeApproximately(200, 0.01f);
        canvas.LastMatrix.TransY.Should().BeApproximately(150, 0.01f);
    }

    #endregion

    #region Multi-Layer Round-Trip Tests

    [Fact]
    public void RoundTrip_MultipleLayersClear_AllCreated()
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Encode 3 layers, all cleared
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 3);

        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 1);
        offset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.AsSpan(offset), 100, 100, 50);

        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteDrawRect(buffer.AsSpan(offset), 200, 200, 100, 100);

        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 2, 1);
        offset += VectorGraphicsEncoderV2.WriteDrawLine(buffer.AsSpan(offset), 0, 0, 300, 300);

        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();
        result.LayerCount.Should().Be(3);

        stage.ClearedLayers.Should().BeEquivalentTo(new byte[] { 0, 1, 2 });
        stage.RemainedLayers.Should().BeEmpty();

        stage.GetCanvas(0).DrawCalls[0].Type.Should().Be("DrawCircle");
        stage.GetCanvas(1).DrawCalls[0].Type.Should().Be("DrawRect");
        stage.GetCanvas(2).DrawCalls[0].Type.Should().Be("DrawLine");
    }

    [Fact]
    public void RoundTrip_MixedClearRemain_CorrectLayerHandling()
    {
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);

        // Frame 1: All layers cleared
        var buffer1 = new byte[1024];
        int offset1 = 0;
        offset1 += VectorGraphicsEncoderV2.WriteMessageHeader(buffer1.AsSpan(offset1), 1, 3);
        offset1 += VectorGraphicsEncoderV2.WriteLayerClear(buffer1.AsSpan(offset1), 0);
        offset1 += VectorGraphicsEncoderV2.WriteLayerClear(buffer1.AsSpan(offset1), 1);
        offset1 += VectorGraphicsEncoderV2.WriteLayerClear(buffer1.AsSpan(offset1), 2);
        offset1 += VectorGraphicsEncoderV2.WriteEndMarker(buffer1.AsSpan(offset1));

        var result1 = decoder.Decode(buffer1.AsSpan(0, offset1));
        result1.Success.Should().BeTrue();
        stage.ClearedLayers.Should().BeEquivalentTo(new byte[] { 0, 1, 2 });

        // Frame 2: Layer 0 cleared, Layer 1&2 remain
        var buffer2 = new byte[1024];
        int offset2 = 0;
        offset2 += VectorGraphicsEncoderV2.WriteMessageHeader(buffer2.AsSpan(offset2), 2, 3);
        offset2 += VectorGraphicsEncoderV2.WriteLayerMaster(buffer2.AsSpan(offset2), 0, 1);
        offset2 += VectorGraphicsEncoderV2.WriteDrawCircle(buffer2.AsSpan(offset2), 50, 50, 25);
        offset2 += VectorGraphicsEncoderV2.WriteLayerRemain(buffer2.AsSpan(offset2), 1);
        offset2 += VectorGraphicsEncoderV2.WriteLayerRemain(buffer2.AsSpan(offset2), 2);
        offset2 += VectorGraphicsEncoderV2.WriteEndMarker(buffer2.AsSpan(offset2));

        var result2 = decoder.Decode(buffer2.AsSpan(0, offset2));
        result2.Success.Should().BeTrue();
        stage.ClearedLayers.Should().ContainSingle().Which.Should().Be(0);
        stage.RemainedLayers.Should().BeEquivalentTo(new byte[] { 1, 2 });
    }

    [Fact]
    public void RoundTrip_RemainWithoutPriorClear_Fails()
    {
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);

        // Try to Remain layer 5 which was never created
        var buffer = new byte[1024];
        int offset = 0;
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), 5);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // This should fail
        var action = () => decoder.Decode(buffer.AsSpan(0, offset));
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Remain failed*layer 5*");
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public void RoundTrip_AllDrawTypes_InSingleFrame()
    {
        var buffer = new byte[4096];
        int offset = 0;

        var pentagon = new SKPoint[5];
        for (int i = 0; i < 5; i++)
        {
            float angle = MathF.PI * 2 * i / 5 - MathF.PI / 2;
            pentagon[i] = new SKPoint(50 * MathF.Cos(angle), 50 * MathF.Sin(angle));
        }

        // Encode all draw types
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 10);

        // Circle
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(255, 0, 0));
        offset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.AsSpan(offset), 100, 100, 50);

        // Rectangle
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(0, 255, 0));
        offset += VectorGraphicsEncoderV2.WriteDrawRect(buffer.AsSpan(offset), 200, 50, 100, 100);

        // Line
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(0, 0, 255));
        offset += VectorGraphicsEncoderV2.WriteDrawLine(buffer.AsSpan(offset), 350, 50, 450, 150);

        // Polygon
        offset += VectorGraphicsEncoderV2.WriteSetStroke(buffer.AsSpan(offset), new RgbColor(255, 255, 0));
        offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.AsSpan(offset), pentagon);

        // Text
        offset += VectorGraphicsEncoderV2.WriteSetFontSize(buffer.AsSpan(offset), 20);
        offset += VectorGraphicsEncoderV2.WriteDrawText(buffer.AsSpan(offset), "All Types!", 100, 250);

        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify all draw types present
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        canvas.DrawCalls.Select(c => c.Type).Should().BeEquivalentTo(
            new[] { "DrawCircle", "DrawRect", "DrawLine", "DrawPolygon", "DrawText" });
    }

    [Fact]
    public void RoundTrip_MultipleFrames_BytesConsumedCorrect()
    {
        // Encode two frames concatenated in one buffer
        var buffer = new byte[4096];
        int totalOffset = 0;

        // Frame 1
        int frame1Start = totalOffset;
        totalOffset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(totalOffset), 1, 1);
        totalOffset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(totalOffset), 0, 1);
        totalOffset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.AsSpan(totalOffset), 50, 50, 25);
        totalOffset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(totalOffset));
        int frame1Size = totalOffset - frame1Start;

        // Frame 2
        int frame2Start = totalOffset;
        totalOffset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(totalOffset), 2, 1);
        totalOffset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(totalOffset), 0, 1);
        totalOffset += VectorGraphicsEncoderV2.WriteDrawRect(buffer.AsSpan(totalOffset), 100, 100, 50, 50);
        totalOffset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(totalOffset));
        int frame2Size = totalOffset - frame2Start;

        // Decode frame 1
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);

        var result1 = decoder.Decode(buffer.AsSpan(0, totalOffset));
        result1.Success.Should().BeTrue();
        result1.FrameId.Should().Be(1);
        result1.BytesConsumed.Should().Be(frame1Size);

        // Decode frame 2 from remaining buffer
        var result2 = decoder.Decode(buffer.AsSpan(frame1Size, frame2Size));
        result2.Success.Should().BeTrue();
        result2.FrameId.Should().Be(2);
        result2.BytesConsumed.Should().Be(frame2Size);
    }

    [Fact]
    public void RoundTrip_LargePolygon_DeltaEncodingWorks()
    {
        var buffer = new byte[8192];
        int offset = 0;

        // Generate a large polygon (500 points in a circle)
        var points = new SKPoint[500];
        for (int i = 0; i < 500; i++)
        {
            float angle = MathF.PI * 2 * i / 500;
            points[i] = new SKPoint(
                400 + 300 * MathF.Cos(angle),
                400 + 300 * MathF.Sin(angle));
        }

        // Encode
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 1);
        offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.AsSpan(offset), points);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);
        var decodedPoints = (SKPoint[])canvas.DrawCalls[0].Params[0];
        decodedPoints.Should().HaveCount(500);

        // Verify a few points (allowing for float->int conversion in encoding)
        for (int i = 0; i < 500; i += 50)
        {
            decodedPoints[i].X.Should().BeApproximately(points[i].X, 1f, $"Point {i} X");
            decodedPoints[i].Y.Should().BeApproximately(points[i].Y, 1f, $"Point {i} Y");
        }
    }

    [Fact]
    public void RoundTrip_NegativeCoordinates_Correct()
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Encode with negative coordinates
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 3);
        offset += VectorGraphicsEncoderV2.WriteDrawCircle(buffer.AsSpan(offset), -100, -200, 50);
        offset += VectorGraphicsEncoderV2.WriteDrawLine(buffer.AsSpan(offset), -500, -300, 500, 300);
        offset += VectorGraphicsEncoderV2.WriteDrawRect(buffer.AsSpan(offset), -50, -50, 100, 100);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        // Decode
        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        // Verify
        result.Success.Should().BeTrue();

        var canvas = stage.GetCanvas(0);

        // Circle
        canvas.DrawCalls[0].Params[0].Should().Be(-100);
        canvas.DrawCalls[0].Params[1].Should().Be(-200);

        // Line
        canvas.DrawCalls[1].Params[0].Should().Be(-500);
        canvas.DrawCalls[1].Params[1].Should().Be(-300);

        // Rect
        canvas.DrawCalls[2].Params[0].Should().Be(-50);
        canvas.DrawCalls[2].Params[1].Should().Be(-50);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RoundTrip_EmptyFrame_NoLayers()
    {
        var buffer = new byte[100];
        int offset = 0;

        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 0);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        result.Success.Should().BeTrue();
        result.LayerCount.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_LayerWithNoOperations_Works()
    {
        var buffer = new byte[100];
        int offset = 0;

        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), 0, 0); // 0 operations
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        result.Success.Should().BeTrue();
        stage.GetCanvas(0).DrawCalls.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_HighLayerId_Works()
    {
        var buffer = new byte[100];
        int offset = 0;

        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 1);
        offset += VectorGraphicsEncoderV2.WriteLayerClear(buffer.AsSpan(offset), 15); // Max valid layer
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        result.Success.Should().BeTrue();
        stage.ClearedLayers.Should().ContainSingle().Which.Should().Be(15);
    }

    [Fact]
    public void RoundTrip_MaxFrameId_Works()
    {
        var buffer = new byte[100];
        int offset = 0;

        ulong maxFrameId = ulong.MaxValue;

        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), maxFrameId, 0);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        var stage = new RecordingStage();
        var decoder = new VectorStreamDecoder(stage);
        var result = decoder.Decode(buffer.AsSpan(0, offset));

        result.Success.Should().BeTrue();
        result.FrameId.Should().Be(maxFrameId);
    }

    #endregion
}
