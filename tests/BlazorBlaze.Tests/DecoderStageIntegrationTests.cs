using System.Collections.Concurrent;
using BlazorBlaze.ValueTypes;
using BlazorBlaze.VectorGraphics.Protocol;
using SkiaSharp;

namespace BlazorBlaze.Tests;

/// <summary>
/// Integration tests for VectorGraphicsDecoderV2 + RenderingStage using real types.
/// No mocks - tests the actual RefArray, Lease, Ref interaction.
/// </summary>
public class DecoderStageIntegrationTests
{
    /// <summary>
    /// Simple layer pool that tracks rent/return for verification.
    /// </summary>
    private class TestLayerPool : ILayerPool
    {
        private readonly int _width;
        private readonly int _height;
        public int RentCount;
        public int ReturnCount;
        public ConcurrentBag<ILayer> AllLayers = new();

        public TestLayerPool(int width = 100, int height = 100)
        {
            _width = width;
            _height = height;
        }

        public Lease<ILayer> Rent(byte layerId)
        {
            Interlocked.Increment(ref RentCount);
            var layer = new LayerCanvas(_width, _height, layerId);
            AllLayers.Add(layer);
            return new Lease<ILayer>(layer, Return);
        }

        private void Return(ILayer layer)
        {
            Interlocked.Increment(ref ReturnCount);
            layer.Dispose(); // Actually dispose since we're not pooling
        }

        public bool AllReturned => RentCount == ReturnCount;
    }

    #region Helper Methods

    /// <summary>
    /// Encodes a simple frame with Clear on specified layers and optional draw operations.
    /// </summary>
    private static byte[] EncodeFrame(ulong frameId, params (byte layerId, bool isClear, SKPoint[]? polygon)[] layers)
    {
        var buffer = new byte[4096];
        int offset = 0;

        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), frameId, (byte)layers.Length);

        foreach (var (layerId, isClear, polygon) in layers)
        {
            if (isClear)
            {
                if (polygon != null && polygon.Length > 0)
                {
                    // Master frame with draw operation
                    offset += VectorGraphicsEncoderV2.WriteLayerMaster(buffer.AsSpan(offset), layerId, 1);
                    offset += VectorGraphicsEncoderV2.WriteDrawPolygon(buffer.AsSpan(offset), polygon);
                }
                else
                {
                    // Clear frame (no operations)
                    offset += VectorGraphicsEncoderV2.WriteLayerClear(buffer.AsSpan(offset), layerId);
                }
            }
            else
            {
                // Remain frame
                offset += VectorGraphicsEncoderV2.WriteLayerRemain(buffer.AsSpan(offset), layerId);
            }
        }

        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));

        return buffer[..offset];
    }

    /// <summary>
    /// Gets an ILayer from a frame copy at specified index.
    /// </summary>
    private static ILayer? GetLayer(RefArray<Lease<ILayer>>? frame, int index)
    {
        if (!frame.HasValue) return null;
        var arr = frame.GetValueOrDefault();
        var lease = arr[index];
        return lease.IsEmpty ? null : lease.Value;
    }

    /// <summary>
    /// Safely disposes a frame copy.
    /// </summary>
    private static void DisposeFrame(ref RefArray<Lease<ILayer>>? frame)
    {
        if (frame.HasValue)
        {
            var arr = frame.GetValueOrDefault();
            arr.Dispose();
            frame = null;
        }
    }

    #endregion

    #region Basic Decode Tests

    [Fact]
    public void Decode_SingleFrame_CreatesLayers()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        // Encode frame with 3 layers
        var message = EncodeFrame(1,
            (0, true, null),
            (1, true, null),
            (2, true, null));

        var result = decoder.Decode(message);

        result.Success.Should().BeTrue();
        result.FrameId.Should().Be(1);
        result.LayerCount.Should().Be(3);
        pool.RentCount.Should().Be(3);

        // Get frame copy
        stage.TryCopyFrame(out var copy).Should().BeTrue();
        GetLayer(copy, 0).Should().NotBeNull();
        GetLayer(copy, 1).Should().NotBeNull();
        GetLayer(copy, 2).Should().NotBeNull();

        DisposeFrame(ref copy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue();
    }

    [Fact]
    public void Decode_MultipleFrames_RemainSharesLayers()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);
        RefArray<Lease<ILayer>>? rendererCopy = null;

        // Frame 1: All layers clear
        var message1 = EncodeFrame(1,
            (0, true, null),
            (1, true, null));

        decoder.Decode(message1);
        stage.TryCopyFrame(out var copy1);
        rendererCopy = copy1;

        pool.RentCount.Should().Be(2);

        // Frame 2: Layer 0 clear, Layer 1 remain
        var message2 = EncodeFrame(2,
            (0, true, null),
            (1, false, null)); // Remain

        decoder.Decode(message2);
        stage.TryCopyFrame(out var copy2);
        DisposeFrame(ref rendererCopy);
        rendererCopy = copy2;

        // Only 1 new rent (layer 0), layer 1 was retained
        pool.RentCount.Should().Be(3);
        pool.ReturnCount.Should().Be(1); // Old layer 0 returned

        DisposeFrame(ref rendererCopy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue();
    }

    [Fact]
    public void Decode_FrameWithDrawOperations_CanRender()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        // Triangle polygon
        var triangle = new SKPoint[] { new(10, 10), new(50, 10), new(30, 50) };

        var message = EncodeFrame(1,
            (0, true, triangle));

        var result = decoder.Decode(message);

        result.Success.Should().BeTrue();

        stage.TryCopyFrame(out var copy);
        var layer = GetLayer(copy, 0);
        layer.Should().NotBeNull();
        layer!.LayerId.Should().Be(0);

        DisposeFrame(ref copy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue();
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ConcurrentDecoderRenderer_NoLeaks()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);
        var running = true;
        var framesDecoded = 0;
        var framesCopied = 0;
        var errors = new ConcurrentBag<string>();

        // Decoder task - simulates incoming frames
        var decoderTask = Task.Run(() =>
        {
            var triangle = new SKPoint[] { new(10, 10), new(50, 10), new(30, 50) };

            for (ulong f = 1; f <= 100; f++)
            {
                byte[] message;
                if (f == 1)
                {
                    // First frame: all clear
                    message = EncodeFrame(f,
                        (0, true, triangle),
                        (1, true, null),
                        (2, true, null));
                }
                else
                {
                    // Subsequent frames: layer 0 clear, others remain
                    message = EncodeFrame(f,
                        (0, true, triangle),
                        (1, false, null),
                        (2, false, null));
                }

                var result = decoder.Decode(message);
                if (!result.Success)
                    errors.Add($"Frame {f} decode failed");

                Interlocked.Increment(ref framesDecoded);
                Thread.Sleep(1);
            }
            running = false;
        });

        // Renderer task - simulates render loop
        var rendererTask = Task.Run(() =>
        {
            RefArray<Lease<ILayer>>? rendererCopy = null;

            while (running || rendererCopy == null)
            {
                if (stage.TryCopyFrame(out var newCopy))
                {
                    DisposeFrame(ref rendererCopy);
                    rendererCopy = newCopy;

                    // Verify layers are accessible
                    for (byte i = 0; i < 3; i++)
                    {
                        var layer = GetLayer(rendererCopy, i);
                        if (layer == null && framesCopied > 0)
                            errors.Add($"Layer {i} is null in copied frame");
                    }

                    Interlocked.Increment(ref framesCopied);
                }
                Thread.Sleep(2);
            }

            DisposeFrame(ref rendererCopy);
        });

        await Task.WhenAll(decoderTask, rendererTask);
        stage.Dispose();

        errors.Should().BeEmpty();
        framesDecoded.Should().Be(100);
        framesCopied.Should().BeGreaterThan(10);
        pool.AllReturned.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleRenderers_AllGetValidFrames()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);
        var running = true;
        var errors = new ConcurrentBag<string>();
        var barrier = new Barrier(4); // 1 decoder + 3 renderers

        // Decoder
        var decoderTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            var triangle = new SKPoint[] { new(10, 10), new(50, 10), new(30, 50) };

            for (ulong f = 1; running && f <= 50; f++)
            {
                var message = f == 1
                    ? EncodeFrame(f, (0, true, triangle), (1, true, null))
                    : EncodeFrame(f, (0, true, triangle), (1, false, null));

                decoder.Decode(message);
                Thread.Sleep(2);
            }
            running = false;
        });

        // Multiple renderers
        var rendererTasks = Enumerable.Range(0, 3).Select(id => Task.Run(() =>
        {
            barrier.SignalAndWait();
            RefArray<Lease<ILayer>>? copy = null;
            var framesReceived = 0;

            while (running || framesReceived == 0)
            {
                if (stage.TryCopyFrame(out var newCopy))
                {
                    DisposeFrame(ref copy);
                    copy = newCopy;
                    framesReceived++;

                    // Simulate render work
                    var layer0 = GetLayer(copy, 0);
                    var layer1 = GetLayer(copy, 1);

                    if (layer0 == null)
                        errors.Add($"Renderer {id}: Layer 0 null");
                    if (layer1 == null && framesReceived > 1)
                        errors.Add($"Renderer {id}: Layer 1 null");
                }
                Thread.Sleep(3);
            }

            DisposeFrame(ref copy);
        })).ToArray();

        await Task.WhenAll(new[] { decoderTask }.Concat(rendererTasks));
        stage.Dispose();

        errors.Should().BeEmpty();
        pool.AllReturned.Should().BeTrue();
    }

    #endregion

    #region Remain Chain Tests

    [Fact]
    public void RemainChain_LayerPersistsAcrossFrames()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);
        RefArray<Lease<ILayer>>? rendererCopy = null;

        // Frame 1: Create layer 0 and 1
        var message1 = EncodeFrame(1, (0, true, null), (1, true, null));
        decoder.Decode(message1);

        stage.TryCopyFrame(out var copy1);
        rendererCopy = copy1;
        var originalLayer1 = GetLayer(rendererCopy, 1);

        pool.RentCount.Should().Be(2);

        // Frames 2-5: Layer 0 clears each time, Layer 1 remains
        for (ulong f = 2; f <= 5; f++)
        {
            var message = EncodeFrame(f, (0, true, null), (1, false, null));
            decoder.Decode(message);

            stage.TryCopyFrame(out var copy);
            DisposeFrame(ref rendererCopy);
            rendererCopy = copy;

            // Same layer 1 instance across all frames
            GetLayer(rendererCopy, 1).Should().BeSameAs(originalLayer1);
        }

        // 2 initial + 4 for layer 0 in frames 2-5 = 6 rents total
        pool.RentCount.Should().Be(6);

        DisposeFrame(ref rendererCopy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue();
    }

    #endregion

    #region Design.md Verification Tests

    /// <summary>
    /// Verifies exact ref count tracking as described in design.md.
    /// When frame has 2 layers and renderer copies frame, each layer's Ref should have refcount=2.
    /// </summary>
    [Fact]
    public void RefCount_FrameWithRendererCopy_HasCorrectCounts()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        // Frame 1: Create 2 layers
        var message = EncodeFrame(1, (0, true, null), (1, true, null));
        decoder.Decode(message);

        pool.RentCount.Should().Be(2, "2 layers created");

        // Renderer copies frame - this should increment ref counts
        stage.TryCopyFrame(out var rendererCopy).Should().BeTrue();

        // At this point:
        // - Stage holds frame with layers (refcount on each Ref = 1)
        // - Renderer holds copy (refcount on each Ref = 2)
        // We can't directly check Ref.RefCount, but we can verify behavior

        // Decode new frame - this will dispose stage's old frame
        var message2 = EncodeFrame(2, (0, true, null), (1, true, null));
        decoder.Decode(message2);

        // Old layers should NOT be returned yet because renderer still holds copy
        pool.ReturnCount.Should().Be(0, "renderer still holds reference to old layers");

        // Now dispose renderer's copy
        DisposeFrame(ref rendererCopy);

        // Now old layers should be returned
        pool.ReturnCount.Should().Be(2, "old layers returned after renderer released");

        stage.Dispose();
        pool.AllReturned.Should().BeTrue();
    }

    /// <summary>
    /// Verifies frame skip behavior: when decoder is faster than renderer,
    /// renderer should get the latest frame, not intermediate ones.
    /// </summary>
    [Fact]
    public void FrameSkip_DecoderFasterThanRenderer_RendererGetsLatest()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        // Decode 3 frames rapidly without renderer copying
        var message1 = EncodeFrame(1, (0, true, null));
        var message2 = EncodeFrame(2, (0, true, null));
        var message3 = EncodeFrame(3, (0, true, null));

        decoder.Decode(message1);
        decoder.Decode(message2);
        decoder.Decode(message3);

        // Intermediate frames were replaced, so only frame 3's layer exists
        // Frames 1 and 2's layers should have been returned to pool
        pool.RentCount.Should().Be(3, "3 layers rented total");
        pool.ReturnCount.Should().Be(2, "frames 1 and 2 layers returned");

        // Renderer copies - gets frame 3
        stage.TryCopyFrame(out var copy);

        DisposeFrame(ref copy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that layer removal returns to pool only when all references disposed.
    /// Scenario: Layer exists in frame, renderer copies, layer removed in next frame.
    /// </summary>
    [Fact]
    public void LayerRemoval_ReturnsToPool_WhenLastRefDisposed()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);
        RefArray<Lease<ILayer>>? rendererCopy = null;

        // Frame 1: 2 layers
        var message1 = EncodeFrame(1, (0, true, null), (1, true, null));
        decoder.Decode(message1);
        pool.RentCount.Should().Be(2);

        // Renderer copies frame (holds references to both layers)
        stage.TryCopyFrame(out var copy1);
        rendererCopy = copy1;

        // Frame 2: Only layer 0 (layer 1 removed)
        var message2 = EncodeFrame(2, (0, false, null)); // Only layer 0 remains
        decoder.Decode(message2);

        // Layer 1 should NOT be returned yet - renderer still has reference
        pool.ReturnCount.Should().Be(0, "renderer still holds layer 1");

        // Renderer disposes old copy
        DisposeFrame(ref rendererCopy);

        // Now layer 1 should be returned
        pool.ReturnCount.Should().Be(1, "layer 1 returned after renderer released");

        // Get new frame
        stage.TryCopyFrame(out var copy2);
        rendererCopy = copy2;

        DisposeFrame(ref rendererCopy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that renderer can re-render the same frame when decoder is slow.
    /// The frame copy should remain valid for multiple render passes.
    /// </summary>
    [Fact]
    public void SlowDecoder_RendererCanRerenderSameFrame()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        // Single frame
        var message = EncodeFrame(1, (0, true, null), (1, true, null));
        decoder.Decode(message);

        // Renderer copies frame
        stage.TryCopyFrame(out var copy);

        // Simulate multiple render passes using the same copy
        for (int renderPass = 0; renderPass < 5; renderPass++)
        {
            // Access layers - should work every time
            var layer0 = GetLayer(copy, 0);
            var layer1 = GetLayer(copy, 1);

            layer0.Should().NotBeNull($"render pass {renderPass}");
            layer1.Should().NotBeNull($"render pass {renderPass}");
        }

        // No new frames decoded, so no returns yet
        pool.ReturnCount.Should().Be(0);

        DisposeFrame(ref copy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue();
    }

    /// <summary>
    /// Verifies the detailed 4-frame flow from design.md (lines 472-606).
    /// This test follows the exact scenario described in the design document.
    /// </summary>
    [Fact]
    public void DesignMdFlow_FourFrameScenario()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);
        RefArray<Lease<ILayer>>? rendererCopy = null;

        // ========== FRAME 0 ==========
        // Protocol: Clear layer 0, Clear layer 1, Clear layer 2
        var frame0 = EncodeFrame(0,
            (0, true, null),   // Clear layer 0
            (1, true, null),   // Clear layer 1
            (2, true, null));  // Clear layer 2
        decoder.Decode(frame0);

        pool.RentCount.Should().Be(3, "Frame 0: 3 layers rented");
        pool.ReturnCount.Should().Be(0, "Frame 0: nothing returned yet");

        // Renderer copies Frame 0
        stage.TryCopyFrame(out var copy0);
        rendererCopy = copy0;

        var layer0_f0 = GetLayer(rendererCopy, 0);
        var layer1_f0 = GetLayer(rendererCopy, 1);
        var layer2_f0 = GetLayer(rendererCopy, 2);

        layer0_f0.Should().NotBeNull();
        layer1_f0.Should().NotBeNull();
        layer2_f0.Should().NotBeNull();

        // ========== FRAME 1 ==========
        // Protocol: Clear layer 0 (new), Remain layer 1, Remain layer 2
        var frame1 = EncodeFrame(1,
            (0, true, null),   // Clear layer 0 (new layer)
            (1, false, null),  // Remain layer 1
            (2, false, null)); // Remain layer 2
        decoder.Decode(frame1);

        pool.RentCount.Should().Be(4, "Frame 1: +1 for new layer 0");
        pool.ReturnCount.Should().Be(0, "Frame 1: old layer 0 not returned yet (renderer holds it)");

        // Renderer finishes with Frame 0, copies Frame 1
        DisposeFrame(ref rendererCopy);

        // Now old layer 0 from frame 0 should be returned
        pool.ReturnCount.Should().Be(1, "old layer 0 returned after renderer released Frame 0");

        stage.TryCopyFrame(out var copy1);
        rendererCopy = copy1;

        // Verify layer 1 and 2 are the same objects (Remain)
        GetLayer(rendererCopy, 1).Should().BeSameAs(layer1_f0, "layer 1 remained");
        GetLayer(rendererCopy, 2).Should().BeSameAs(layer2_f0, "layer 2 remained");
        GetLayer(rendererCopy, 0).Should().NotBeSameAs(layer0_f0, "layer 0 is new");

        // ========== FRAME 2 ==========
        // Protocol: Clear layer 0 (new), Clear layer 1 (new), Remain layer 2
        var frame2 = EncodeFrame(2,
            (0, true, null),   // Clear layer 0 (new)
            (1, true, null),   // Clear layer 1 (new)
            (2, false, null)); // Remain layer 2
        decoder.Decode(frame2);

        pool.RentCount.Should().Be(6, "Frame 2: +2 for new layer 0 and layer 1");
        pool.ReturnCount.Should().Be(1, "Frame 2: old layers not returned yet (renderer holds Frame 1)");

        // Renderer disposes Frame 1, copies Frame 2
        DisposeFrame(ref rendererCopy);

        // Old layer 0 and layer 1 from Frame 1 should be returned
        pool.ReturnCount.Should().Be(3, "old layer 0 (Frame 1) and layer 1 returned");

        stage.TryCopyFrame(out var copy2);
        rendererCopy = copy2;

        // Layer 2 still the same (remained through all frames)
        GetLayer(rendererCopy, 2).Should().BeSameAs(layer2_f0, "layer 2 remained through all frames");

        // ========== FRAME 3 ==========
        // Protocol: Clear layer 0, Remain layer 1, (layer 2 removed)
        var frame3 = EncodeFrame(3,
            (0, true, null),   // Clear layer 0
            (1, false, null)); // Remain layer 1 (layer 2 removed)
        decoder.Decode(frame3);

        pool.RentCount.Should().Be(7, "Frame 3: +1 for new layer 0");

        // Renderer disposes Frame 2, copies Frame 3
        DisposeFrame(ref rendererCopy);

        // Layer 2 (original from Frame 0) should now be returned
        // Also old layer 0 from Frame 2

        stage.TryCopyFrame(out var copy3);
        rendererCopy = copy3;

        GetLayer(rendererCopy, 0).Should().NotBeNull();
        GetLayer(rendererCopy, 1).Should().NotBeNull();
        GetLayer(rendererCopy, 2).Should().BeNull("layer 2 was removed in Frame 3");

        // Cleanup
        DisposeFrame(ref rendererCopy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue("all layers eventually returned");
    }

    /// <summary>
    /// Verifies that multiple TryCopy calls each increment ref count.
    /// </summary>
    [Fact]
    public void MultipleTryCopy_EachIncrementsRefCount()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        // Single frame
        var message = EncodeFrame(1, (0, true, null));
        decoder.Decode(message);

        pool.RentCount.Should().Be(1);

        // Multiple renderer copies
        stage.TryCopyFrame(out var copy1);
        stage.TryCopyFrame(out var copy2);
        stage.TryCopyFrame(out var copy3);

        // All copies should reference the same layer
        var layer1 = GetLayer(copy1, 0);
        var layer2 = GetLayer(copy2, 0);
        var layer3 = GetLayer(copy3, 0);

        layer1.Should().BeSameAs(layer2);
        layer2.Should().BeSameAs(layer3);

        // Dispose stage first (simulating decoder shutdown)
        stage.Dispose();

        // Layer should NOT be returned yet - copies still hold references
        pool.ReturnCount.Should().Be(0, "copies still hold references");

        // Dispose copies one by one
        DisposeFrame(ref copy1);
        pool.ReturnCount.Should().Be(0, "copy2 and copy3 still hold references");

        DisposeFrame(ref copy2);
        pool.ReturnCount.Should().Be(0, "copy3 still holds reference");

        DisposeFrame(ref copy3);
        pool.ReturnCount.Should().Be(1, "all references released, layer returned");

        pool.AllReturned.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that stage can be disposed while renderer still holds a copy.
    /// The copy should remain valid until disposed.
    /// </summary>
    [Fact]
    public void StageDispose_WhileRendererHoldsCopy_CopyRemainsValid()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        var message = EncodeFrame(1, (0, true, null), (1, true, null));
        decoder.Decode(message);

        // Renderer copies
        stage.TryCopyFrame(out var copy);

        // Dispose stage (simulating decoder thread shutdown)
        stage.Dispose();

        // Copy should still be valid
        GetLayer(copy, 0).Should().NotBeNull("copy remains valid after stage dispose");
        GetLayer(copy, 1).Should().NotBeNull("copy remains valid after stage dispose");

        // Layers not returned yet
        pool.ReturnCount.Should().Be(0);

        // Dispose copy
        DisposeFrame(ref copy);

        pool.AllReturned.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyFrame_NoLayers()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        // Frame with 0 layers
        var buffer = new byte[20];
        int offset = 0;
        offset += VectorGraphicsEncoderV2.WriteMessageHeader(buffer.AsSpan(offset), 1, 0);
        offset += VectorGraphicsEncoderV2.WriteEndMarker(buffer.AsSpan(offset));
        var message = buffer[..offset];

        var result = decoder.Decode(message);

        result.Success.Should().BeTrue();
        result.LayerCount.Should().Be(0);
        pool.RentCount.Should().Be(0);

        stage.Dispose();
    }

    [Fact]
    public void HighLayerId_WorksCorrectly()
    {
        var pool = new TestLayerPool();
        var stage = new RenderingStage(100, 100, pool);
        var decoder = new VectorStreamDecoder(stage);

        // Use layer IDs 0, 5, 15 (sparse)
        var message = EncodeFrame(1,
            (0, true, null),
            (5, true, null),
            (15, true, null));

        var result = decoder.Decode(message);

        result.Success.Should().BeTrue();
        pool.RentCount.Should().Be(3);

        stage.TryCopyFrame(out var copy);
        GetLayer(copy, 0).Should().NotBeNull();
        GetLayer(copy, 5).Should().NotBeNull();
        GetLayer(copy, 15).Should().NotBeNull();
        GetLayer(copy, 1).Should().BeNull(); // Unused slot

        DisposeFrame(ref copy);
        stage.Dispose();

        pool.AllReturned.Should().BeTrue();
    }

    #endregion
}
