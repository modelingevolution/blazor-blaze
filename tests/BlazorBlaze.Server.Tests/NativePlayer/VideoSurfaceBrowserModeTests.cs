using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.EventAggregator;
using NSubstitute;
using EventAggregator = ModelingEvolution.EventAggregator.EventAggregator;

namespace BlazorBlaze.Server.Tests.NativePlayer;

/// <summary>
/// Tests B-020 through B-024: VideoSurface browser mode (IsKiosk == false).
/// </summary>
public sealed class VideoSurfaceBrowserModeTests : BunitContext
{
    private readonly EventAggregator _ea = new(new NullForwarder(), new EventAggregatorPool());

    public VideoSurfaceBrowserModeTests()
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(false);
        Services.AddSingleton(kioskDetector);
        Services.AddSingleton<IEventAggregator>(_ea);
    }

    /// <summary>B-020: Renders img with correct src</summary>
    [Fact]
    public void BrowserMode_RendersImgWithCorrectSrc()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost:5001/mjpeg"));

        var img = cut.Find("img");
        img.GetAttribute("src").Should().Be("http://localhost:5001/mjpeg");
    }

    /// <summary>B-021: Renders img with passed Style and Class</summary>
    [Fact]
    public void BrowserMode_RendersImgWithStyleAndClass()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Style, "max-width:100%")
            .Add(vs => vs.Class, "video-preview"));

        var img = cut.Find("img");
        img.GetAttribute("style").Should().Contain("max-width:100%");
        img.GetAttribute("class").Should().Contain("video-preview");
    }

    /// <summary>B-022: No invisible placeholder element</summary>
    [Fact]
    public void BrowserMode_DoesNotRenderPlaceholderDiv()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        cut.FindAll("div").Should().BeEmpty();
    }

    /// <summary>B-023: No native player messages sent (no JS module loaded)</summary>
    [Fact]
    public void BrowserMode_NoJsModuleImported()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        JSInterop.Invocations.Should().BeEmpty();
    }

    /// <summary>B-024: Does not publish PlayerInitialized in browser mode</summary>
    [Fact]
    public void BrowserMode_DoesNotPublishPlayerInitialized()
    {
        var published = new List<PlayerInitialized>();
        _ea.GetEvent<PlayerInitialized>().Subscribe(e => published.Add(e));

        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        published.Should().BeEmpty();
    }

    /// <summary>B-020 (alt): Renders img alt text</summary>
    [Fact]
    public void BrowserMode_RendersImgWithAltText()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Alt, "Camera feed"));

        var img = cut.Find("img");
        img.GetAttribute("alt").Should().Be("Camera feed");
    }

    /// <summary>B-020 (default alt): Uses default alt text</summary>
    [Fact]
    public void BrowserMode_DefaultAltText()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var img = cut.Find("img");
        img.GetAttribute("alt").Should().Be("Video stream");
    }
}
