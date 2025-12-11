using Microsoft.Extensions.Logging;
using BlazorBlaze.VectorGraphics.Protocol;

namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// Factory methods for creating rendering streams.
/// </summary>
public static class RenderingStreams
{
    /// <summary>
    /// Create a rendering stream for VectorGraphics protocol.
    /// This is the built-in protocol for general-purpose vector streaming.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for diagnostics</param>
    /// <param name="options">Optional configuration options</param>
    /// <returns>A new IRenderingStream instance</returns>
    public static IRenderingStream VectorGraphics(
        ILoggerFactory loggerFactory,
        VectorGraphicsOptions? options = null)
    {
        options ??= VectorGraphicsOptions.Default;
        return new RenderingStream(
            new VectorGraphicsDecoder(options),
            loggerFactory,
            options.MaxBufferSize);
    }
}
