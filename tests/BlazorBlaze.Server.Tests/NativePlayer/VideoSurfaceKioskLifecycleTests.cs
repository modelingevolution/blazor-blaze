using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.EventAggregator;
using NSubstitute;
using EventAggregator = ModelingEvolution.EventAggregator.EventAggregator;

namespace BlazorBlaze.Server.Tests.NativePlayer;

public sealed class VideoSurfaceKioskLifecycleTests : BunitContext
{
    private readonly List<PlayerInitialized> _initializedEvents = [];
    private readonly List<PlayerDestroyed> _destroyedEvents = [];
    private readonly List<PlayRequested> _playRequestedEvents = [];
    private readonly List<BackgroundColorChanged> _bgColorEvents = [];
    private readonly BunitJSModuleInterop _moduleInterop;

    private readonly EventAggregator _ea = new(new NullForwarder(), new EventAggregatorPool());

    public VideoSurfaceKioskLifecycleTests()
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        Services.AddSingleton(kioskDetector);
        Services.AddSingleton<IEventAggregator>(_ea);

        _moduleInterop = JSInterop.SetupModule("./_content/BlazorBlaze.Server/video-surface.js");
        _moduleInterop.Mode = JSRuntimeMode.Loose;

        _ea.GetEvent<PlayerInitialized>().Subscribe(e => _initializedEvents.Add(e));
        _ea.GetEvent<PlayerDestroyed>().Subscribe(e => _destroyedEvents.Add(e));
        _ea.GetEvent<PlayRequested>().Subscribe(e => _playRequestedEvents.Add(e));
        _ea.GetEvent<BackgroundColorChanged>().Subscribe(e => _bgColorEvents.Add(e));
    }

    [Fact]
    public void KioskMode_ImportsModuleAndCreatesAdapterOnFirstRender()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _moduleInterop.VerifyInvoke("createAdapter");
        _initializedEvents.Should().ContainSingle();
    }

    [Fact]
    public void KioskMode_CreateAdapterReceivesPlayerId()
    {
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "my-player"));

        var invocation = _moduleInterop.VerifyInvoke("createAdapter");
        invocation.Arguments[1].Should().Be("my-player");
    }

    [Fact]
    public void KioskMode_CreateAdapterReceivesExactlyThreeArgs()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var invocation = _moduleInterop.VerifyInvoke("createAdapter");
        invocation.Arguments.Should().HaveCount(3);
        invocation.Arguments[2].Should().NotBeNull();
    }

    [Fact]
    public void KioskMode_PublishesPlayRequestedAfterInit()
    {
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        _initializedEvents.Should().ContainSingle(e => e.Id == "test-player");
        _playRequestedEvents.Should().ContainSingle(e => e.Id == "test-player");
    }

    [Fact]
    public void KioskMode_DoesNotInvokeNativeInitOrPlay()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var forbidden = JSInterop.Invocations
            .Where(i => i.Identifier is "init" or "play" or "pause" or "resume"
                or "refresh" or "destroy" or "setBackgroundColor" or "getBackgroundColor")
            .ToArray();
        forbidden.Should().BeEmpty();
    }

    [Fact]
    public async Task KioskMode_PublishesPlayerDestroyedOnDispose()
    {
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        _initializedEvents.Should().ContainSingle();

        await DisposeComponentsAsync();

        _destroyedEvents.Should().ContainSingle(e => e.Id == "test-player");

        var disposeInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "dispose")
            .ToArray();
        disposeInvocations.Should().ContainSingle();
    }

    [Fact]
    public void KioskMode_PlayerInitialized_ContainsPlayerId()
    {
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        _initializedEvents.Should().ContainSingle();
        _initializedEvents[0].Id.Should().Be("test-player");
    }

    [Fact]
    public void KioskMode_DisposeAsync_CatchesJSDisconnectedException()
    {
        var ctx = new BunitContext();
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        ctx.Services.AddSingleton(kioskDetector);
        ctx.Services.AddEventAggregator();

        var moduleInterop = ctx.JSInterop.SetupModule("./_content/BlazorBlaze.Server/video-surface.js");
        moduleInterop.Mode = JSRuntimeMode.Loose;

        ctx.Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var act = () => ctx.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void KioskMode_DoubleDispose_DoesNotThrow()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var act = () =>
        {
            cut.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void KioskMode_AutoGeneratesPlayerId()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _initializedEvents.Should().ContainSingle();
        _initializedEvents[0].Id.Should().StartWith("vs-");
    }

    [Fact]
    public void KioskMode_BackgroundColor_PublishesBackgroundColorChanged()
    {
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.BackgroundColor, "#000000"));

        _bgColorEvents.Should().ContainSingle(e => e.Color == "#000000");
        _initializedEvents.Should().ContainSingle();
    }

    [Fact]
    public void KioskMode_NoBackgroundColor_DoesNotPublishBackgroundColorChanged()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _initializedEvents.Should().ContainSingle();
        _bgColorEvents.Should().BeEmpty();
    }
}
