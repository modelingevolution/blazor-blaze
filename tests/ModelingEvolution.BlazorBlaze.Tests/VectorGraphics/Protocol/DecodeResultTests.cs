using ModelingEvolution.BlazorBlaze.VectorGraphics.Protocol;

namespace ModelingEvolution.BlazorBlaze.Tests.VectorGraphics.Protocol;

public class DecodeResultTests
{
    [Fact]
    public void NeedMoreData_WithDefaultConsumed_ReturnsExpectedResult()
    {
        var result = DecodeResult.NeedMoreData();

        result.Success.Should().BeFalse();
        result.BytesConsumed.Should().Be(0);
        result.FrameNumber.Should().BeNull();
    }

    [Fact]
    public void NeedMoreData_WithConsumedBytes_ReturnsExpectedResult()
    {
        var result = DecodeResult.NeedMoreData(consumed: 42);

        result.Success.Should().BeFalse();
        result.BytesConsumed.Should().Be(42);
        result.FrameNumber.Should().BeNull();
    }

    [Fact]
    public void Frame_ReturnsSuccessResult()
    {
        var result = DecodeResult.Frame(frameNumber: 12345, consumed: 100);

        result.Success.Should().BeTrue();
        result.FrameNumber.Should().Be(12345UL);
        result.BytesConsumed.Should().Be(100);
    }

    [Fact]
    public void Frame_WithZeroFrameNumber_IsValid()
    {
        var result = DecodeResult.Frame(frameNumber: 0, consumed: 10);

        result.Success.Should().BeTrue();
        result.FrameNumber.Should().Be(0UL);
        result.BytesConsumed.Should().Be(10);
    }

    [Fact]
    public void Frame_WithMaxFrameNumber_IsValid()
    {
        var result = DecodeResult.Frame(frameNumber: ulong.MaxValue, consumed: 50);

        result.Success.Should().BeTrue();
        result.FrameNumber.Should().Be(ulong.MaxValue);
        result.BytesConsumed.Should().Be(50);
    }

    [Fact]
    public void DecodeResult_IsRecordStruct_SupportsEquality()
    {
        var result1 = DecodeResult.Frame(100, 50);
        var result2 = DecodeResult.Frame(100, 50);
        var result3 = DecodeResult.Frame(101, 50);

        result1.Should().Be(result2);
        result1.Should().NotBe(result3);
    }

    [Fact]
    public void DecodeResult_IsReadonly_CanBeUsedInSpans()
    {
        // This test verifies the struct can be used efficiently
        var results = new DecodeResult[3];
        results[0] = DecodeResult.NeedMoreData();
        results[1] = DecodeResult.Frame(1, 10);
        results[2] = DecodeResult.Frame(2, 20);

        results.Should().HaveCount(3);
        results[0].Success.Should().BeFalse();
        results[1].Success.Should().BeTrue();
        results[2].FrameNumber.Should().Be(2UL);
    }

    [Fact]
    public void DecodeResult_WithInit_CanBeConstructedManually()
    {
        var result = new DecodeResult
        {
            Success = true,
            BytesConsumed = 123,
            FrameNumber = 456
        };

        result.Success.Should().BeTrue();
        result.BytesConsumed.Should().Be(123);
        result.FrameNumber.Should().Be(456UL);
    }

    [Fact]
    public void DecodeResult_Default_IsUnsuccess()
    {
        var result = default(DecodeResult);

        result.Success.Should().BeFalse();
        result.BytesConsumed.Should().Be(0);
        result.FrameNumber.Should().BeNull();
    }
}
