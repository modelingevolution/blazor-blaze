using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorBlaze.Tests.VectorGraphics;

public class RenderingStreamFactoryTests
{
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    [Fact]
    public void CreateVectorGraphicsStream_ReturnsValidStream()
    {
        // Arrange & Act
        var stream = RenderingStreamFactory.CreateVectorGraphicsStream(800, 600, _loggerFactory);

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void CreateVectorGraphicsStream_WithCustomBufferSize_ReturnsStream()
    {
        // Arrange & Act
        var stream = RenderingStreamFactory.CreateVectorGraphicsStream(800, 600, _loggerFactory, 16 * 1024 * 1024);

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void RenderingStreamV2_WithCustomDecoder_CreatesStream()
    {
        // Arrange
        var pool = new LayerPool(800, 600);
        var stage = new RenderingStage(800, 600, pool);
        var decoder = new TestDecoder(stage);

        // Act
        var stream = new RenderingStreamV2(stage, pool, decoder, _loggerFactory, 8 * 1024 * 1024);

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void RenderingStreamV2_SimpleConstructor_CreatesStream()
    {
        // Arrange & Act
        var stream = new RenderingStreamV2(1200, 800, _loggerFactory);

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void RenderingStreamV2_MultipleInstances_AreIndependent()
    {
        // Arrange & Act
        var stream1 = new RenderingStreamV2(800, 600, _loggerFactory);
        var stream2 = new RenderingStreamV2(800, 600, _loggerFactory);

        // Assert
        Assert.NotSame(stream1, stream2);
    }

    /// <summary>
    /// Test decoder implementation for unit tests.
    /// </summary>
    private class TestDecoder : IFrameDecoder
    {
        public IStage Stage { get; }

        public TestDecoder(IStage stage)
        {
            Stage = stage;
        }

        public DecodeResultV2 Decode(ReadOnlySpan<byte> data)
        {
            // Simple test implementation - just return need more data
            return DecodeResultV2.NeedMoreData;
        }
    }
}
