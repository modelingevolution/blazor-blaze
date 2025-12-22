using ModelingEvolution;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// Combines multiple RenderingStreamV2 instances into a single composited output.
/// Each stream runs independently with its own stage/pool.
/// Z-order is determined by the order streams are added (first = back).
/// </summary>
public class CompositeRenderingStream : IAsyncDisposable
{
    private readonly List<RenderingStreamV2> _streams = new();
    private bool _disposed;

    /// <summary>
    /// Number of streams in this composite.
    /// </summary>
    public int Count => _streams.Count;

    /// <summary>
    /// True if all streams are connected.
    /// </summary>
    public bool IsConnected => _streams.Count > 0 && _streams.All(s => s.IsConnected);

    /// <summary>
    /// True if any stream is connected.
    /// </summary>
    public bool IsAnyConnected => _streams.Any(s => s.IsConnected);

    /// <summary>
    /// Adds a stream to the composite. Streams render in add order (first = back).
    /// </summary>
    public void AddStream(RenderingStreamV2 stream)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CompositeRenderingStream));

        _streams.Add(stream);
    }

    /// <summary>
    /// Disconnects all streams.
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        foreach (var stream in _streams)
        {
            if (stream.IsConnected)
                await stream.DisconnectAsync();
        }
    }

    /// <summary>
    /// Renders all streams to the canvas in order (first stream = back).
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        foreach (var stream in _streams)
            stream.Render(canvas);
    }

    /// <summary>
    /// Gets the total frame count across all streams.
    /// </summary>
    public ulong TotalFrames => _streams.Aggregate(0ul, (sum, s) => sum + s.Frame);

    /// <summary>
    /// Gets the minimum FPS across all streams (bottleneck indicator).
    /// </summary>
    public float MinFps => _streams.Count > 0
        ? _streams.Min(s => s.Fps)
        : 0;

    /// <summary>
    /// Gets the total transfer rate across all streams.
    /// </summary>
    public Bytes TotalTransferRate => _streams.Aggregate(
        Bytes.Zero,
        (sum, s) => sum + s.TransferRate);

    /// <summary>
    /// Gets individual stream stats.
    /// </summary>
    public IReadOnlyList<(ulong Frame, float Fps, Bytes TransferRate, bool IsConnected)> StreamStats
        => _streams.Select(s => (s.Frame, s.Fps, s.TransferRate, s.IsConnected)).ToList();

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var stream in _streams)
            await stream.DisposeAsync();

        _streams.Clear();
    }
}
