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

        _ea.GetEvent<ViewportChanged>().Subscribe(e => _viewportEvents.Add(e));
    }

    [Fact]
    public void Fill_KioskPlaceholder_FitsInsideContainerBothAxes()
    {
        // Arrange & Act
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.Fill, true)
            .Add(vs => vs.FrameWidth, 1280)
            .Add(vs => vs.FrameHeight, 720));

        // Assert — fit-contain: aspect ratio preserved, capped in BOTH axes (review #6),
        // with no unconditional full width that would overflow a constrained container.
        var style = cut.Find("div").GetAttribute("style") ?? "";
        style.Should().Contain("aspect-ratio:1280 / 720");
        style.Should().Contain("max-width:100%");
        style.Should().Contain("max-height:100%");
        style.Should().NotContain("; width:100%");
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
        evt.NPanX.Should().Be(0.25);
        evt.NPanY.Should().Be(-0.5);
    }

    [Fact]
    public void ViewportChanged_WireTypeName_IsViewportChanged()
    {
        // Arrange
        var registry = new NativeCppEventRegistry(Array.Empty<Type>());

        // Act
        var name = registry.GetTypeName(typeof(ViewportChanged));

        // Assert — native dispatches on this exact string (pinned wire contract).
        name.Should().Be("viewport-changed");
    }

    [Fact]
    public void ViewportChanged_WireFieldNames_MatchPinnedContract()
    {
        // Arrange — same camelCase policy NativeCppForwarder serializes with.
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(
            new ViewportChanged("preview-1", 2.0, 0.25, -0.5), options);

        // Assert — { id, scale, nPanX, nPanY } per the pinned wire contract.
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var props = doc.RootElement.EnumerateObject().Select(p => p.Name);
        props.Should().BeEquivalentTo("id", "scale", "nPanX", "nPanY");
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
