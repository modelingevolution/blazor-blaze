using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.EventAggregator;
using NSubstitute;
using EventAggregator = ModelingEvolution.EventAggregator.EventAggregator;

namespace BlazorBlaze.Server.Tests.NativePlayer;

public sealed class VideoSurfaceNativeStreamUrlTests : BunitContext
{
    private readonly List<PlayerInitialized> _initializedEvents = [];
    private readonly EventAggregator _ea = new(new NullForwarder(), new EventAggregatorPool());

    private void SetupKiosk(bool isKiosk)
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(isKiosk);
        Services.AddSingleton(kioskDetector);
        Services.AddSingleton<IEventAggregator>(_ea);

        if (isKiosk)
        {
            var moduleInterop = JSInterop.SetupModule("./_content/BlazorBlaze.Server/video-surface.js");
            moduleInterop.Mode = JSRuntimeMode.Loose;
        }

        _ea.GetEvent<PlayerInitialized>().Subscribe(e => _initializedEvents.Add(e));
    }

    [Fact]
    public void KioskMode_PublishesNativeStreamUrl_WhenSet()
    {
        // Arrange
        SetupKiosk(true);

        // Act
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://host:5001/mjpeg")
            .Add(vs => vs.NativeStreamUrl, "shm://cam0"));

        // Assert
        _initializedEvents.Should().ContainSingle();
        _initializedEvents[0].Url.Should().Be("shm://cam0");
    }

    [Fact]
    public void KioskMode_DoesNotRenderBrowserImg_WhenNativeStreamUrlSet()
    {
        // Arrange
        SetupKiosk(true);

        // Act
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://host:5001/mjpeg")
            .Add(vs => vs.NativeStreamUrl, "shm://cam0"));

        // Assert
        cut.FindAll("img").Should().BeEmpty();
    }

    [Fact]
    public void BrowserMode_ImgUsesStreamUrl_NeverNativeStreamUrl()
    {
        // Arrange
        SetupKiosk(false);

        // Act
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://host:5001/mjpeg")
            .Add(vs => vs.NativeStreamUrl, "shm://cam0"));

        // Assert
        var img = cut.Find("img");
        img.GetAttribute("src").Should().Be("http://host:5001/mjpeg");
        cut.Markup.Should().NotContain("shm://");
    }

    [Fact]
    public void BrowserMode_PublishesNoPlayerInitialized_WhenNativeStreamUrlSet()
    {
        // Arrange
        SetupKiosk(false);

        // Act
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://host:5001/mjpeg")
            .Add(vs => vs.NativeStreamUrl, "shm://cam0"));

        // Assert
        _initializedEvents.Should().BeEmpty();
    }

    [Fact]
    public void KioskMode_FallsBackToStreamUrl_WhenNativeStreamUrlUnset()
    {
        // Arrange
        SetupKiosk(true);

        // Act
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://host:5001/mjpeg"));

        // Assert
        _initializedEvents.Should().ContainSingle();
        _initializedEvents[0].Url.Should().Be("http://host:5001/mjpeg");
    }
}
