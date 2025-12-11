using ModelingEvolution;
using SkiaSharp;

namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// A stream that produces rendering commands from an external data source.
/// Abstracts protocol details and provides a consistent rendering API.
/// </summary>
public interface IRenderingStream : IAsyncDisposable
{
    /// <summary>
    /// Connect to the data source.
    /// </summary>
    Task ConnectAsync(Uri uri, CancellationToken ct = default);

    /// <summary>
    /// Disconnect from the data source.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Whether the stream is currently connected and receiving data.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Current frame number.
    /// </summary>
    ulong Frame { get; }

    /// <summary>
    /// Frames per second (measured).
    /// </summary>
    float Fps { get; }

    /// <summary>
    /// Last error message, if any.
    /// </summary>
    string? Error { get; }

    /// <summary>
    /// Data transfer rate in bytes per second.
    /// </summary>
    Bytes TransferRate { get; }

    /// <summary>
    /// Render current state to canvas.
    /// Thread-safe - can be called from render loop.
    /// </summary>
    void Render(SKCanvas canvas);
}
