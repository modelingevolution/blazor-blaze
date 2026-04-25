using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.EventAggregator;
using NSubstitute;
using EventAggregator = ModelingEvolution.EventAggregator.EventAggregator;

namespace BlazorBlaze.Server.Tests.NativePlayer;

public sealed class VideoSurfaceKioskRenderingTests : BunitContext
{
    public VideoSurfaceKioskRenderingTests()
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        Services.AddSingleton(kioskDetector);
        Services.AddSingleton<IEventAggregator>(new EventAggregator(new NullForwarder(), new EventAggregatorPool()));

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void KioskMode_RendersPlaceholderDiv()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        cut.Find("div").Should().NotBeNull();
    }

    [Fact]
    public void KioskMode_PlaceholderHasHiddenVisibility()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var div = cut.Find("div");
        var style = div.GetAttribute("style") ?? "";
        style.Should().Contain("visibility:hidden");
        style.Should().Contain("opacity:0");
    }

    [Fact]
    public void KioskMode_PlaceholderHasCorrectDimensions()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Width, 1280)
            .Add(vs => vs.Height, 720));

        var div = cut.Find("div");
        var style = div.GetAttribute("style") ?? "";
        style.Should().Contain("width:1280px");
        style.Should().Contain("height:720px");
    }

    [Fact]
    public void KioskMode_PlaceholderNotDisplayNone()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var div = cut.Find("div");
        var style = div.GetAttribute("style") ?? "";
        style.Should().NotContain("display:none");
        style.Should().NotContain("display: none");
    }

    [Fact]
    public void KioskMode_DoesNotRenderImgElement()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        cut.FindAll("img").Should().BeEmpty();
    }

    [Fact]
    public void KioskMode_DefaultDimensionsAre1920x1080()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var div = cut.Find("div");
        var style = div.GetAttribute("style") ?? "";
        style.Should().Contain("width:1920px");
        style.Should().Contain("height:1080px");
    }
}
