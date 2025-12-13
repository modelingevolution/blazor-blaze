using System.Collections.Immutable;
using BlazorBlaze.ValueTypes;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Interface for layer pools.
/// </summary>
public interface ILayerPool
{
    Lease<ILayer> Rent(byte layerId);
}

/// <summary>
/// Stage implementation that manages layers with lock-free rendering.
///
/// Decoder thread calls: OnFrameStart, Clear, Remain, this[layerId], OnFrameEnd
/// Renderer thread calls: TryCopyFrame
/// </summary>
public class RenderingStage : IStage, IDisposable
{
    private readonly ILayerPool _pool;

    // Working state - index = layerId, O(1) access, no sorting needed
    private readonly Ref<Lease<ILayer>>?[] _workingLayers = new Ref<Lease<ILayer>>?[16];

    // Frame state
    private ValueTypes.SpinLock _frameLock;
    private RefArray<Lease<ILayer>> _displayFrame;
    private RefArray<Lease<ILayer>> _prevFrame;

    private ulong _currentFrameId;
    private bool _disposed;

    public RenderingStage(int width, int height, ILayerPool pool)
    {
        _pool = pool;
    }

    public ulong CurrentFrameId => _currentFrameId;

    // --- Decoder Thread API ---

    public ICanvas this[byte layerId]
    {
        get
        {
            var layerRef = _workingLayers[layerId];
            if (layerRef == null)
                throw new InvalidOperationException($"Layer {layerId} not found. Call Clear or Remain first.");

            return layerRef.Value.Value.Canvas;
        }
    }

    public void OnFrameStart(ulong frameId)
    {
        _currentFrameId = frameId;
        Array.Clear(_workingLayers);
    }

    public void Clear(byte layerId)
    {
        var lease = _pool.Rent(layerId);
        lease.Value.Clear();
        var layerRef = new Ref<Lease<ILayer>>(lease);
        _workingLayers[layerId] = layerRef;
    }

    public void Remain(byte layerId)
    {
        var prevRef = _prevFrame.GetRef(layerId);
        if (prevRef == null || !prevRef.TryCopy(out var copy))
            throw new InvalidOperationException($"Remain failed for layer {layerId}");

        _workingLayers[layerId] = copy!;
    }

    public void OnFrameEnd()
    {
        // Build ImmutableArray directly from working array (already ordered by index)
        var builder = ImmutableArray.CreateBuilder<Ref<Lease<ILayer>>?>(_workingLayers.Length);
        for (int i = 0; i < _workingLayers.Length; i++)
            builder.Add(_workingLayers[i]);

        _prevFrame.Dispose();
        _prevFrame = new RefArray<Lease<ILayer>>(builder.MoveToImmutable());

        _frameLock.Enter();
        _displayFrame.Dispose();
        if (_prevFrame.TryCopy(out var tmp))
            _displayFrame = tmp!.Value;
        _frameLock.Exit();

        Array.Clear(_workingLayers);
    }

    // --- Renderer Thread API ---

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose any remaining working layers
        for (int i = 0; i < _workingLayers.Length; i++)
        {
            _workingLayers[i]?.Dispose();
            _workingLayers[i] = null;
        }

        _prevFrame.Dispose();
        _displayFrame.Dispose();
    }
}
