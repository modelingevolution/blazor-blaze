using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.EventAggregator;
using ModelingEvolution.EventAggregator.Blazor;
using NSubstitute;
using EventAggregator = ModelingEvolution.EventAggregator.EventAggregator;

namespace BlazorBlaze.Server.Tests.NativePlayer;

public sealed class VideoSurfacePreviewTests : BunitContext
{
    private readonly List<PlayerInitialized> _initializedEvents = [];
    private readonly List<ViewportChanged> _viewportEvents = [];
    private readonly BunitJSModuleInterop _moduleInterop;
    private readonly EventAggregator _ea = new(new NullForwarder(), new EventAggregatorPool());

    public VideoSurfacePreviewTests()
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        Services.AddSingleton(kioskDetector);
        Services.AddSingleton<IEventAggregator>(_ea);

        _moduleInterop = JSInterop.SetupModule("./_content/BlazorBlaze.Server/video-surface.js");
        _moduleInterop.Mode = JSRuntimeMode.Loose;

        _ea.GetEvent<PlayerInitialized>().Subscribe(e => _initializedEvents.Add(e));
        _ea.GetEvent<ViewportChanged>().Subscribe(e => _viewportEvents.Add(e));
    }

    [Fact]
    public void Fill_KioskPlaceholder_IsAspectBoundAndFullWidth()
    {
        // Arrange & Act
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Fill, true)
            .Add(vs => vs.FrameWidth, 1280)
            .Add(vs => vs.FrameHeight, 720));

        // Assert
        var style = cut.Find("div").GetAttribute("style") ?? "";
        style.Should().Contain("width:100%");
        style.Should().Contain("aspect-ratio:1280 / 720");
    }

    [Fact]
    public void Fill_KioskPlaceholder_PaintsNoOpaquePixels()
    {
        // Arrange & Act
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Fill, true)
            .Add(vs => vs.FrameWidth, 1920)
            .Add(vs => vs.FrameHeight, 1080));

        // Assert
        var style = cut.Find("div").GetAttribute("style") ?? "";
        style.Should().Contain("visibility:hidden");
        style.Should().Contain("opacity:0");
    }

    [Fact]
    public void Fill_DoesNotRenderImgPoll()
    {
        // Arrange & Act
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Fill, true)
            .Add(vs => vs.FrameWidth, 1280)
            .Add(vs => vs.FrameHeight, 720));

        // Assert
        cut.FindAll("img").Should().BeEmpty();
    }

    [Fact]
    public void Fill_FallsBackToWidthHeight_WhenFrameDimensionsZero()
    {
        // Arrange & Act
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Fill, true)
            .Add(vs => vs.Width, 800)
            .Add(vs => vs.Height, 600));

        // Assert
        var style = cut.Find("div").GetAttribute("style") ?? "";
        style.Should().Contain("aspect-ratio:800 / 600");
    }

    [Fact]
    public void NonFill_KioskPlaceholder_KeepsFixedPixelSize()
    {
        // Arrange & Act
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Width, 1280)
            .Add(vs => vs.Height, 720));

        // Assert
        var style = cut.Find("div").GetAttribute("style") ?? "";
        style.Should().Contain("width:1280px");
        style.Should().Contain("height:720px");
        style.Should().NotContain("aspect-ratio");
    }

    [Fact]
    public void PlainVideo_True_CarriedOnPlayerInitialized()
    {
        // Arrange & Act
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlainVideo, true));

        // Assert
        _initializedEvents.Should().ContainSingle();
        _initializedEvents[0].PlainVideo.Should().BeTrue();
    }

    [Fact]
    public void PlainVideo_DefaultsFalse_OnPlayerInitialized()
    {
        // Arrange & Act
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        // Assert
        _initializedEvents.Should().ContainSingle();
        _initializedEvents[0].PlainVideo.Should().BeFalse();
    }

    [Fact]
    public async Task PublishViewportAsync_Kiosk_PublishesViewportChangedWithResolvedId()
    {
        // Arrange
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "preview-1"));

        // Act
        await cut.InvokeAsync(() => cut.Instance.PublishViewportAsync(2.0, 0.25, -0.5));

        // Assert
        _viewportEvents.Should().ContainSingle();
        var evt = _viewportEvents[0];
        evt.Id.Should().Be("preview-1");
        evt.Scale.Should().Be(2.0);
        evt.PanX.Should().Be(0.25);
        evt.PanY.Should().Be(-0.5);
    }

    [Fact]
    public void ViewportChanged_WireTypeName_IsTransformChanged()
    {
        // Arrange
        var registry = new NativeCppEventRegistry(Array.Empty<Type>());

        // Act
        var name = registry.GetTypeName(typeof(ViewportChanged));

        // Assert — native dispatches on this exact string (wire-contract.md §1).
        name.Should().Be("transform-changed");
    }
}

public sealed class VideoSurfaceBrowserViewportTests : BunitContext
{
    private readonly EventAggregator _ea = new(new NullForwarder(), new EventAggregatorPool());
    private readonly List<ViewportChanged> _viewportEvents = [];

    public VideoSurfaceBrowserViewportTests()
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(false);
        Services.AddSingleton(kioskDetector);
        Services.AddSingleton<IEventAggregator>(_ea);
        _ea.GetEvent<ViewportChanged>().Subscribe(e => _viewportEvents.Add(e));
    }

    [Fact]
    public async Task PublishViewportAsync_Browser_PublishesNothing()
    {
        // Arrange
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        // Act
        await cut.InvokeAsync(() => cut.Instance.PublishViewportAsync(2.0, 0.1, 0.1));

        // Assert — browser applies the transform itself; no native consumer exists.
        _viewportEvents.Should().BeEmpty();
    }
}
