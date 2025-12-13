using BlazorBlaze.VectorGraphics.Protocol;
using Microsoft.Extensions.Logging;

namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// Builder for RenderingStreamV2 with pluggable decoder.
/// Allows injecting custom decoders for different binary protocols.
/// </summary>
public class RenderingStreamBuilder
{
    private readonly int _width;
    private readonly int _height;
    private readonly ILoggerFactory _loggerFactory;
    private int _maxBufferSize = 8 * 1024 * 1024;
    private Func<IStage, IFrameDecoder>? _decoderFactory;

    public RenderingStreamBuilder(int width, int height, ILoggerFactory loggerFactory)
    {
        _width = width;
        _height = height;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Sets the maximum buffer size for WebSocket receive buffer.
    /// Default is 8 MB.
    /// </summary>
    public RenderingStreamBuilder WithMaxBufferSize(int size)
    {
        _maxBufferSize = size;
        return this;
    }

    /// <summary>
    /// Sets a custom decoder factory. The factory receives the IStage
    /// and should return an IFrameDecoder implementation.
    /// </summary>
    /// <example>
    /// var stream = new RenderingStreamBuilder(1200, 800, loggerFactory)
    ///     .WithDecoder(stage => new SegmentationDecoder(stage, colorPalette))
    ///     .Build();
    /// </example>
    public RenderingStreamBuilder WithDecoder(Func<IStage, IFrameDecoder> decoderFactory)
    {
        _decoderFactory = decoderFactory;
        return this;
    }

    /// <summary>
    /// Builds the RenderingStreamV2 instance.
    /// If no custom decoder is specified, uses VectorStreamDecoder (Protocol V2).
    /// </summary>
    public RenderingStreamV2 Build()
    {
        var pool = new LayerPool(_width, _height);
        var stage = new RenderingStage(_width, _height, pool);
        var decoder = _decoderFactory?.Invoke(stage) ?? new VectorStreamDecoder(stage);

        return new RenderingStreamV2(stage, pool, decoder, _loggerFactory, _maxBufferSize);
    }
}
