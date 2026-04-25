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
    }

    [Fact]
    public void KioskMode_ImportsModuleAndCreatesAdapterOnFirstRender()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _moduleInterop.VerifyInvoke("createAdapter");

        var initInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "init")
            .ToArray();
        initInvocations.Should().ContainSingle();
        initInvocations[0].Arguments[0].Should().Be("http://localhost/stream");
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
    public void KioskMode_CreateAdapterReceivesExactlyTwoArgs()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var invocation = _moduleInterop.VerifyInvoke("createAdapter");
        invocation.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void KioskMode_PublishesPlayerInitializedAfterInitAndPlay()
    {
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        _initializedEvents.Should().ContainSingle(e => e.Id == "test-player");

        var initInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "init")
            .ToArray();
        initInvocations.Should().ContainSingle();
        initInvocations[0].Arguments[0].Should().Be("http://localhost/stream");

        var playInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "play")
            .ToArray();
        playInvocations.Should().ContainSingle();
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

        var destroyInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "destroy")
            .ToArray();
        destroyInvocations.Should().ContainSingle();
    }

    [Fact]
    public void KioskMode_PlayerInitializedPublishedOnRender()
    {
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        _initializedEvents.Should().ContainSingle(e => e.Id == "test-player");
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
    public void KioskMode_BackgroundColor_LifecycleCompletes()
    {
        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.BackgroundColor, "#000000"));

        _initializedEvents.Should().ContainSingle();

        var getInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "getBackgroundColor")
            .ToArray();
        getInvocations.Should().ContainSingle();

        var setInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "setBackgroundColor")
            .ToArray();
        setInvocations.Should().ContainSingle();
        setInvocations[0].Arguments[0].Should().Be("#000000");
    }

    [Fact]
    public async Task KioskMode_BackgroundColor_DisposeRestoresPreviousColor()
    {
        _moduleInterop.Setup<string>("getBackgroundColor").SetResult("#ffffff");

        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.BackgroundColor, "#000000"));

        _initializedEvents.Should().ContainSingle();

        await DisposeComponentsAsync();

        _destroyedEvents.Should().ContainSingle();

        var setInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "setBackgroundColor")
            .ToArray();
        setInvocations.Should().HaveCount(2);
        setInvocations[0].Arguments[0].Should().Be("#000000");
        setInvocations[1].Arguments[0].Should().Be("#ffffff");
    }

    [Fact]
    public void KioskMode_NoBackgroundColor_LifecycleCompletes()
    {
        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _initializedEvents.Should().ContainSingle();

        var bgInvocations = JSInterop.Invocations
            .Where(i => i.Identifier is "setBackgroundColor" or "getBackgroundColor")
            .ToArray();
        bgInvocations.Should().BeEmpty();
    }
}
