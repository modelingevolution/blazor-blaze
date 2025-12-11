using BlazorBlaze.VectorGraphics;

namespace ModelingEvolution.BlazorBlaze.Tests.VectorGraphics;

public class FpsWatchTests
{
    [Fact]
    public void Value_InitiallyZero()
    {
        var watch = new FpsWatch();

        watch.Value.Should().Be(0);
    }

    [Fact]
    public void Increment_BeforePeriod_DoesNotUpdateValue()
    {
        var watch = new FpsWatch { MeasurePeriod = TimeSpan.FromHours(1) };

        watch++;
        watch++;
        watch++;

        watch.Value.Should().Be(0);
    }

    [Fact]
    public void ExplicitCast_ReturnsValue()
    {
        var watch = new FpsWatch();

        double value = (double)watch;

        value.Should().Be(watch.Value);
    }

    [Fact]
    public void ToString_ReturnsValueAsString()
    {
        var watch = new FpsWatch();

        watch.ToString().Should().Be("0");
    }

    [Fact]
    public void Restart_ResetsStopwatch()
    {
        var watch = new FpsWatch();
        watch++;

        var act = () => watch.Restart();

        act.Should().NotThrow();
    }
}
