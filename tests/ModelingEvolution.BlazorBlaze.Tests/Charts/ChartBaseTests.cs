using ModelingEvolution.BlazorBlaze.Charts;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.Tests.Charts;

public class ChartBaseTests
{
    private class TestChart : ChartBase
    {
        public bool RenderContentCalled { get; private set; }
        public SKCanvas? LastCanvas { get; private set; }

        protected override void RenderContent(SKCanvas canvas)
        {
            RenderContentCalled = true;
            LastCanvas = canvas;
        }
    }

    [Fact]
    public void Location_DefaultsToOrigin()
    {
        using var chart = new TestChart();

        chart.Location.Should().Be(new SKPoint(0, 0));
    }

    [Fact]
    public void Size_DefaultsToZero()
    {
        using var chart = new TestChart();

        chart.Size.Should().Be(new SKSize(0, 0));
    }

    [Fact]
    public void Bounds_CalculatedFromLocationAndSize()
    {
        using var chart = new TestChart
        {
            Location = new SKPoint(10, 20),
            Size = new SKSize(100, 50)
        };

        chart.Bounds.Should().Be(new SKRect(10, 20, 110, 70));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var chart = new TestChart();

        var act = () =>
        {
            chart.Dispose();
            chart.Dispose();
        };

        act.Should().NotThrow();
    }
}

public class TimeSeriesFTests
{
    [Fact]
    public void TimeSeriesF_CanBeCreated()
    {
        var data = new[] { 1.0f, 2.0f, 3.0f };
        var series = new TimeSeriesF
        {
            Label = "Test",
            Data = data,
            Count = 3,
            Color = SKColors.Red
        };

        series.Label.Should().Be("Test");
        series.Count.Should().Be(3);
        series.Color.Should().Be(SKColors.Red);
        series.Data.Should().BeEquivalentTo(data);
    }
}
