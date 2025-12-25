using BlazorBlaze.VectorGraphics.Protocol;
using Microsoft.Extensions.Logging;

namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// Factory for creating RenderingStreamV2 instances with the built-in VectorGraphics decoder.
/// For custom decoders (Segmentation, Keypoints, etc.), use the RenderingStreamV2 constructor directly.
/// </summary>
public static class RenderingStreamFactory
{
    /// <summary>
    /// Creates a RenderingStreamV2 with the built-in VectorGraphics decoder.
    /// </summary>
    /// <param name="width">Canvas width in pixels</param>
    /// <param name="height">Canvas height in pixels</param>
    /// <param name="loggerFactory">Logger factory for diagnostics</param>
    /// <param name="maxBufferSize">WebSocket receive buffer size (default 8 MB)</param>
    public static RenderingStreamV2 CreateVectorGraphicsStream(
        int width,
        int height,
        ILoggerFactory loggerFactory,
        int maxBufferSize = 8 * 1024 * 1024)
    {
        var pool = new LayerPool(width, height);
        var stage = new RenderingStage(width, height, pool);
        var decoder = new VectorStreamDecoder(stage);

        return new RenderingStreamV2(stage, pool, decoder, loggerFactory, maxBufferSize);
    }
}
