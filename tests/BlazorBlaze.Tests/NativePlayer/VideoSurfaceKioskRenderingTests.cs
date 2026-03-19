using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BlazorBlaze.Tests.NativePlayer;

/// <summary>
/// Tests B-030 through B-034: VideoSurface kiosk mode rendering.
/// </summary>
public sealed class VideoSurfaceKioskRenderingTests : BunitContext
{
    public VideoSurfaceKioskRenderingTests()
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        Services.AddSingleton(kioskDetector);
        Services.AddSingleton(Substitute.For<INativePlayerRegistry>());

        // Setup JS interop for kiosk mode (the component will try to import module).
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>B-030: Renders invisible placeholder (not img)</summary>
    [Fact]
    public void KioskMode_RendersPlaceholderDiv()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        cut.Find("div").Should().NotBeNull();
    }

    /// <summary>B-031: Placeholder has visibility:hidden and opacity:0</summary>
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

    /// <summary>B-032: Placeholder has correct width/height from parameters</summary>
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

    /// <summary>B-033: Placeholder maintains layout space (not display:none)</summary>
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

    /// <summary>B-034: No img element rendered in kiosk mode</summary>
    [Fact]
    public void KioskMode_DoesNotRenderImgElement()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        cut.FindAll("img").Should().BeEmpty();
    }

    /// <summary>B-032 (default): Default dimensions are 1920x1080</summary>
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
