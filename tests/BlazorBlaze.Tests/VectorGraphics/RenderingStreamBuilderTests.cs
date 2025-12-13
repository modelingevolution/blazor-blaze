using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorBlaze.Tests.VectorGraphics;

public class RenderingStreamBuilderTests
{
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    [Fact]
    public void Build_WithoutDecoder_UsesVectorStreamDecoder()
    {
        // Arrange & Act
        var stream = new RenderingStreamBuilder(800, 600, _loggerFactory)
            .Build();

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void Build_WithCustomDecoder_UsesCustomDecoder()
    {
        // Arrange
        IFrameDecoder? capturedDecoder = null;

        // Act
        var stream = new RenderingStreamBuilder(800, 600, _loggerFactory)
            .WithDecoder(stage =>
            {
                var decoder = new TestDecoder(stage);
                capturedDecoder = decoder;
                return decoder;
            })
            .Build();

        // Assert
        Assert.NotNull(stream);
        Assert.NotNull(capturedDecoder);
        Assert.IsType<TestDecoder>(capturedDecoder);
    }

    [Fact]
    public void Build_WithMaxBufferSize_SetsBufferSize()
    {
        // Arrange & Act
        var stream = new RenderingStreamBuilder(800, 600, _loggerFactory)
            .WithMaxBufferSize(16 * 1024 * 1024)
            .Build();

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void Build_DecoderReceivesStage()
    {
        // Arrange
        IStage? receivedStage = null;

        // Act
        var stream = new RenderingStreamBuilder(800, 600, _loggerFactory)
            .WithDecoder(stage =>
            {
                receivedStage = stage;
                return new TestDecoder(stage);
            })
            .Build();

        // Assert
        Assert.NotNull(receivedStage);
    }

    [Fact]
    public void Build_FluentApi_ChainsCorrectly()
    {
        // Arrange & Act
        var stream = new RenderingStreamBuilder(1200, 800, _loggerFactory)
            .WithMaxBufferSize(4 * 1024 * 1024)
            .WithDecoder(stage => new TestDecoder(stage))
            .Build();

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void Build_MultipleBuilds_CreateIndependentInstances()
    {
        // Arrange
        var builder = new RenderingStreamBuilder(800, 600, _loggerFactory);

        // Act
        var stream1 = builder.Build();
        var stream2 = builder.Build();

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
