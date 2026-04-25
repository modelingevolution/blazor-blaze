using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.EventAggregator;
using NSubstitute;
using EventAggregator = ModelingEvolution.EventAggregator.EventAggregator;

namespace BlazorBlaze.Server.Tests.NativePlayer;

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

    [Fact]
    public void BrowserMode_RendersImgWithCorrectSrc()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost:5001/mjpeg"));

        var img = cut.Find("img");
        img.GetAttribute("src").Should().Be("http://localhost:5001/mjpeg");
    }

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

    [Fact]
    public void BrowserMode_DoesNotRenderPlaceholderDiv()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        cut.FindAll("div").Should().BeEmpty();
    }

    [Fact]
    public void BrowserMode_NoJsModuleImported()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        JSInterop.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void BrowserMode_DoesNotPublishPlayerInitialized()
    {
        var published = new List<PlayerInitialized>();
        _ea.GetEvent<PlayerInitialized>().Subscribe(e => published.Add(e));

        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        published.Should().BeEmpty();
    }

    [Fact]
    public void BrowserMode_RendersImgWithAltText()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Alt, "Camera feed"));

        var img = cut.Find("img");
        img.GetAttribute("alt").Should().Be("Camera feed");
    }

    [Fact]
    public void BrowserMode_DefaultAltText()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var img = cut.Find("img");
        img.GetAttribute("alt").Should().Be("Video stream");
    }
}
