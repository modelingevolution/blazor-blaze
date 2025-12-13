using System.Collections.Concurrent;
using System.Collections.Immutable;
using BlazorBlaze.ValueTypes;
using BlazorBlaze.VectorGraphics.Protocol;

namespace BlazorBlaze.Tests;

/// <summary>
/// Tests for the RenderingStage pattern using immutable RefArray.
/// Validates the IStage interaction pattern with bucket-based indexing.
/// </summary>
public class RenderingStageTests
{
    /// <summary>
    /// Mock layer for testing - implements ILayer interface.
    /// </summary>
    private class MockLayer : ILayer
    {
        private static int _nextId;

        public int Id { get; } = Interlocked.Increment(ref _nextId);
        public byte LayerId { get; }
        public bool IsDisposed { get; private set; }
        public int DisposeCount { get; private set; }

        // ILayer implementation
        public ICanvas Canvas => throw new NotImplementedException("Mock");

        public MockLayer(byte layerId) => LayerId = layerId;

        public void Clear() { }

        public void DrawTo(SkiaSharp.SKCanvas target) { }

        public void Dispose()
        {
            IsDisposed = true;
            DisposeCount++;
        }
    }

    /// <summary>
    /// Mock pool that tracks rent/return operations.
    /// </summary>
    private class MockPool
    {
        public int RentCount;
        public int ReturnCount;
        public ConcurrentBag<MockLayer> AllCreated = new();

        public Lease<ILayer> Rent(byte layerId)
        {
            Interlocked.Increment(ref RentCount);

            var layer = new MockLayer(layerId);
            AllCreated.Add(layer);

            return new Lease<ILayer>(layer, Return);
        }

        private void Return(ILayer layer)
        {
            Interlocked.Increment(ref ReturnCount);
        }

        public bool AllReturned => RentCount == ReturnCount;
    }

    /// <summary>
    /// Mock RenderingStage using bucket-based array design.
    /// Index = layerId, O(1) access, no sorting needed.
    /// Mirrors the actual RenderingStage implementation.
    /// </summary>
    private class MockRenderingStage
    {
        private readonly MockPool _pool;

        // Working state - index = layerId
        private readonly Ref<Lease<ILayer>>?[] _workingLayers = new Ref<Lease<ILayer>>?[16];

        // Frame state
        private BlazorBlaze.ValueTypes.SpinLock _frameLock;
        private RefArray<Lease<ILayer>> _displayFrame;
        private RefArray<Lease<ILayer>> _prevFrame;

        public int FrameCount { get; private set; }

        public MockRenderingStage(MockPool pool) => _pool = pool;

        public void OnFrameStart()
        {
            Array.Clear(_workingLayers);
        }

        public void Clear(byte layerId)
        {
            var lease = _pool.Rent(layerId);
            var layerRef = new Ref<Lease<ILayer>>(lease);
            _workingLayers[layerId] = layerRef;
        }

        public void Remain(byte layerId)
        {
            var prevRef = _prevFrame.GetRef(layerId);
            if (prevRef == null || !prevRef.TryCopy(out var copy))
                throw new InvalidOperationException($"Remain failed for layer {layerId}");

            _workingLayers[layerId] = copy;
        }

        public ILayer GetLayer(byte layerId)
        {
            var layerRef = _workingLayers[layerId];
            if (layerRef == null)
                throw new InvalidOperationException($"Layer {layerId} not found");
            return layerRef.Value.Value;
        }

        public void OnFrameEnd()
        {
            // Build ImmutableArray from working array
            var builder = ImmutableArray.CreateBuilder<Ref<Lease<ILayer>>?>(_workingLayers.Length);
            for (int i = 0; i < _workingLayers.Length; i++)
                builder.Add(_workingLayers[i]);

            _prevFrame.Dispose();
            _prevFrame = new RefArray<Lease<ILayer>>(builder.MoveToImmutable());

            _frameLock.Enter();
            _displayFrame = _prevFrame;
            _frameLock.Exit();

            FrameCount++;
        }

        public bool TryCopyFrame(out RefArray<Lease<ILayer>>? copy)
        {
            _frameLock.Enter();
            try
            {
                return _displayFrame.TryCopy(out copy);
            }
            finally
            {
                _frameLock.Exit();
            }
        }

        public void Shutdown()
        {
            // Dispose working layers
            for (int i = 0; i < _workingLayers.Length; i++)
            {
                _workingLayers[i]?.Dispose();
                _workingLayers[i] = null;
            }

            _prevFrame.Dispose();
            _displayFrame.Dispose();
        }
    }

    #region Helper Methods

    /// <summary>
    /// Gets the ILayer from a RefArray at the specified index.
    /// </summary>
    private static ILayer? GetLayer(RefArray<Lease<ILayer>>? array, int index)
    {
        if (!array.HasValue) return null;
        var arr = array.GetValueOrDefault();
        var lease = arr[index];
        // Workaround: Lease is a struct, check IsEmpty instead of null
        return lease.IsEmpty ? null : lease.Value;
    }

    /// <summary>
    /// Gets the MockLayer from a RefArray at the specified index (for assertions).
    /// </summary>
    private static MockLayer? GetMockLayer(RefArray<Lease<ILayer>>? array, int index)
    {
        return GetLayer(array, index) as MockLayer;
    }

    /// <summary>
    /// Disposes a nullable RefArray safely.
    /// </summary>
    private static void DisposeFrame(ref RefArray<Lease<ILayer>>? array)
    {
        if (array.HasValue)
        {
            var arr = array.GetValueOrDefault();
            arr.Dispose();
            array = null;
        }
    }

    #endregion

    #region Basic Tests

    [Fact]
    public void SimplifiedStage_ClearLayers_CreatesFrame()
    {
        var pool = new MockPool();
        var stage = new MockRenderingStage(pool);

        stage.OnFrameStart();
        stage.Clear(0);
        stage.Clear(1);
        stage.Clear(2);
        stage.OnFrameEnd();

        pool.RentCount.Should().Be(3);
        stage.FrameCount.Should().Be(1);

        stage.TryCopyFrame(out var copy).Should().BeTrue();
        GetLayer(copy, 0).Should().NotBeNull();
        GetLayer(copy, 1).Should().NotBeNull();
        GetLayer(copy, 2).Should().NotBeNull();

        DisposeFrame(ref copy);
        stage.Shutdown();

        pool.AllReturned.Should().BeTrue();
    }

    [Fact]
    public void SimplifiedStage_RemainLayer_SharesReference()
    {
        var pool = new MockPool();
        var stage = new MockRenderingStage(pool);

        // Frame 0: Create all layers
        stage.OnFrameStart();
        stage.Clear(0);
        stage.Clear(1);
        stage.OnFrameEnd();

        stage.TryCopyFrame(out var copy0);
        var frame0Layer1Id = GetMockLayer(copy0, 1)!.Id;
        DisposeFrame(ref copy0);

        // Frame 1: Layer 0 Clear, Layer 1 Remain
        stage.OnFrameStart();
        stage.Clear(0);
        stage.Remain(1);
        stage.OnFrameEnd();

        stage.TryCopyFrame(out var copy1);
        var frame1Layer1Id = GetMockLayer(copy1, 1)!.Id;

        // Same layer instance should be shared
        frame1Layer1Id.Should().Be(frame0Layer1Id);

        DisposeFrame(ref copy1);
        stage.Shutdown();

        pool.RentCount.Should().Be(3); // 2 in frame0, 1 in frame1 (layer 0 only)
        pool.AllReturned.Should().BeTrue();
    }

    [Fact]
    public void SimplifiedStage_LayerOrdering_IndexedByLayerId()
    {
        var pool = new MockPool();
        var stage = new MockRenderingStage(pool);

        // Add layers (order doesn't matter with bucket design)
        stage.OnFrameStart();
        stage.Clear(2);
        stage.Clear(0);
        stage.Clear(1);
        stage.OnFrameEnd();

        stage.TryCopyFrame(out var copy);

        // Index = layerId in bucket design
        GetMockLayer(copy, 0)!.LayerId.Should().Be(0);
        GetMockLayer(copy, 1)!.LayerId.Should().Be(1);
        GetMockLayer(copy, 2)!.LayerId.Should().Be(2);

        DisposeFrame(ref copy);
        stage.Shutdown();
    }

    #endregion

    #region Renderer Pattern Tests

    [Fact]
    public void Renderer_HoldsUntilNewFrame_NoLeak()
    {
        var pool = new MockPool();
        var stage = new MockRenderingStage(pool);
        RefArray<Lease<ILayer>>? rendererCopy = null;

        // Frame 0
        stage.OnFrameStart();
        stage.Clear(0);
        stage.OnFrameEnd();

        // Renderer gets frame
        stage.TryCopyFrame(out var newCopy);
        rendererCopy = newCopy;

        pool.ReturnCount.Should().Be(0); // Still held

        // Frame 1
        stage.OnFrameStart();
        stage.Clear(0);
        stage.OnFrameEnd();

        // Renderer gets new frame, disposes old
        stage.TryCopyFrame(out newCopy);
        DisposeFrame(ref rendererCopy);
        rendererCopy = newCopy;

        pool.ReturnCount.Should().Be(1); // Frame 0 layer returned

        // Frame 2
        stage.OnFrameStart();
        stage.Clear(0);
        stage.OnFrameEnd();

        // Renderer gets new frame, disposes old
        stage.TryCopyFrame(out newCopy);
        DisposeFrame(ref rendererCopy);
        rendererCopy = newCopy;

        pool.ReturnCount.Should().Be(2); // Frame 1 layer returned

        // Shutdown
        DisposeFrame(ref rendererCopy);
        stage.Shutdown();

        pool.AllReturned.Should().BeTrue();
    }

    [Fact]
    public async Task DecoderRenderer_ConcurrentPattern_NoLeaks()
    {
        var pool = new MockPool();
        var stage = new MockRenderingStage(pool);
        var running = true;
        var framesProduced = 0;
        var framesConsumed = 0;

        // Decoder task
        var decoderTask = Task.Run(() =>
        {
            for (int f = 0; f < 100; f++)
            {
                stage.OnFrameStart();
                stage.Clear(0);
                stage.Clear(1);
                if (f % 3 == 0) stage.Clear(2); // Variable layer count
                stage.OnFrameEnd();

                Interlocked.Increment(ref framesProduced);
                Thread.Sleep(1);
            }
            running = false;
        });

        // Renderer task
        var rendererTask = Task.Run(() =>
        {
            RefArray<Lease<ILayer>>? rendererCopy = null;

            while (running || rendererCopy == null)
            {
                if (stage.TryCopyFrame(out var newCopy))
                {
                    DisposeFrame(ref rendererCopy);
                    rendererCopy = newCopy;
                    Interlocked.Increment(ref framesConsumed);
                }
                Thread.Sleep(2);
            }

            DisposeFrame(ref rendererCopy);
        });

        await Task.WhenAll(decoderTask, rendererTask);
        stage.Shutdown();

        framesProduced.Should().Be(100);
        framesConsumed.Should().BeGreaterThan(0);
        pool.AllReturned.Should().BeTrue();
    }

    #endregion

    #region Remain Chain Tests

    [Fact]
    public void RemainChain_LayerSurvivesMultipleFrames()
    {
        var pool = new MockPool();
        var stage = new MockRenderingStage(pool);
        RefArray<Lease<ILayer>>? rendererCopy = null;

        // Frame 0: All clear
        stage.OnFrameStart();
        stage.Clear(0);
        stage.Clear(1);
        stage.OnFrameEnd();

        stage.TryCopyFrame(out var copy);
        var layer0Id = GetMockLayer(copy, 0)!.Id;
        DisposeFrame(ref rendererCopy);
        rendererCopy = copy;

        pool.RentCount.Should().Be(2);

        // Frames 1-3: Layer 0 remains, Layer 1 clears
        for (int f = 1; f <= 3; f++)
        {
            stage.OnFrameStart();
            stage.Remain(0);
            stage.Clear(1);
            stage.OnFrameEnd();

            stage.TryCopyFrame(out copy);

            // Layer 0 should be the same instance
            GetMockLayer(copy, 0)!.Id.Should().Be(layer0Id);

            DisposeFrame(ref rendererCopy);
            rendererCopy = copy;
        }

        // Only 5 rents: 2 in frame0, 1 each in frames 1-3
        pool.RentCount.Should().Be(5);

        // Cleanup
        DisposeFrame(ref rendererCopy);
        stage.Shutdown();

        pool.AllReturned.Should().BeTrue();
    }

    #endregion

    #region Detailed Flow Test (3 Layers)

    [Fact]
    public void DetailedFlow_3Layers_MatchesDesignDoc()
    {
        var pool = new MockPool();
        var stage = new MockRenderingStage(pool);
        RefArray<Lease<ILayer>>? rendererCopy = null;

        // FRAME 0: All layers new
        stage.OnFrameStart();
        stage.Clear(0);  // L0
        stage.Clear(1);  // L1
        stage.Clear(2);  // L2
        stage.OnFrameEnd();

        stage.TryCopyFrame(out var copy);
        rendererCopy = copy;

        var L0_id = GetMockLayer(rendererCopy, 0)!.Id;
        var L1_id = GetMockLayer(rendererCopy, 1)!.Id;
        var L2_id = GetMockLayer(rendererCopy, 2)!.Id;

        pool.RentCount.Should().Be(3);
        pool.ReturnCount.Should().Be(0);

        // FRAME 1: Layer 0 Remain, Layer 1 Clear, Layer 2 Remain
        stage.OnFrameStart();
        stage.Remain(0);  // L0 stays
        stage.Clear(1);   // L1b new
        stage.Remain(2);  // L2 stays
        stage.OnFrameEnd();

        stage.TryCopyFrame(out copy);
        DisposeFrame(ref rendererCopy);
        rendererCopy = copy;

        // L0 and L2 should be same, L1 is new
        GetMockLayer(rendererCopy, 0)!.Id.Should().Be(L0_id);
        GetMockLayer(rendererCopy, 1)!.Id.Should().NotBe(L1_id);
        GetMockLayer(rendererCopy, 2)!.Id.Should().Be(L2_id);

        var L1b_id = GetMockLayer(rendererCopy, 1)!.Id;

        pool.RentCount.Should().Be(4); // 3 + 1 (L1b)
        pool.ReturnCount.Should().Be(1); // L1 returned

        // FRAME 2: Layer 0 Remain, Layer 1 Remain, Layer 2 Clear
        stage.OnFrameStart();
        stage.Remain(0);  // L0 stays
        stage.Remain(1);  // L1b stays
        stage.Clear(2);   // L2b new
        stage.OnFrameEnd();

        stage.TryCopyFrame(out copy);
        DisposeFrame(ref rendererCopy);
        rendererCopy = copy;

        GetMockLayer(rendererCopy, 0)!.Id.Should().Be(L0_id);
        GetMockLayer(rendererCopy, 1)!.Id.Should().Be(L1b_id);
        GetMockLayer(rendererCopy, 2)!.Id.Should().NotBe(L2_id);

        pool.RentCount.Should().Be(5); // 4 + 1 (L2b)
        pool.ReturnCount.Should().Be(2); // L1, L2 returned

        // FRAME 3: All layers Clear
        stage.OnFrameStart();
        stage.Clear(0);
        stage.Clear(1);
        stage.Clear(2);
        stage.OnFrameEnd();

        stage.TryCopyFrame(out copy);
        DisposeFrame(ref rendererCopy);
        rendererCopy = copy;

        pool.RentCount.Should().Be(8); // 5 + 3
        pool.ReturnCount.Should().Be(5); // L0, L1b, L2b also returned

        // Cleanup
        DisposeFrame(ref rendererCopy);
        stage.Shutdown();

        pool.RentCount.Should().Be(8);
        pool.ReturnCount.Should().Be(8);
        pool.AllReturned.Should().BeTrue();
    }

    #endregion

    #region Stress Test

    [Fact]
    public async Task StressTest_3Layers_10Seconds()
    {
        var pool = new MockPool();
        var stage = new MockRenderingStage(pool);
        var running = true;
        var framesProduced = 0;
        var framesConsumed = 0;
        var errors = new ConcurrentBag<string>();

        var barrier = new Barrier(4); // 1 decoder + 3 renderers

        // Decoder
        var decoderTask = Task.Run(() =>
        {
            barrier.SignalAndWait();

            while (running)
            {
                stage.OnFrameStart();

                // Layer 0: Always Clear
                stage.Clear(0);

                // Layer 1: Always Clear
                stage.Clear(1);

                // Layer 2: Remain (after first frame)
                if (framesProduced == 0)
                    stage.Clear(2);
                else
                    stage.Remain(2);

                stage.OnFrameEnd();
                Interlocked.Increment(ref framesProduced);
                Thread.Sleep(1);
            }
        });

        // Renderers
        var rendererTasks = Enumerable.Range(0, 3).Select(id => Task.Run(() =>
        {
            barrier.SignalAndWait();
            RefArray<Lease<ILayer>>? copy = null;

            while (running)
            {
                if (stage.TryCopyFrame(out var newCopy))
                {
                    DisposeFrame(ref copy);
                    copy = newCopy;

                    // Validate layers 0, 1, 2
                    for (byte i = 0; i < 3; i++)
                    {
                        var layer = GetMockLayer(copy, i);
                        if (layer == null)
                        {
                            errors.Add($"Renderer {id}: Layer {i} is null");
                            continue;
                        }
                        if (layer.IsDisposed)
                            errors.Add($"Renderer {id}: Layer {i} (id={layer.Id}) disposed while held");
                    }

                    Interlocked.Increment(ref framesConsumed);
                }
                Thread.Sleep(2);
            }

            DisposeFrame(ref copy);
        })).ToArray();

        // Run for 10 seconds
        await Task.Delay(TimeSpan.FromSeconds(10));
        running = false;

        await decoderTask;
        await Task.WhenAll(rendererTasks);
        stage.Shutdown();

        // Verify
        errors.Should().BeEmpty();
        framesProduced.Should().BeGreaterThan(100);
        framesConsumed.Should().BeGreaterThan(50);
        pool.AllReturned.Should().BeTrue();
    }

    #endregion
}
