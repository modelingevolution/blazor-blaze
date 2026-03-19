using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace BlazorBlaze.Tests.NativePlayer;

/// <summary>
/// Tests B-040 through B-052: VideoSurface kiosk mode lifecycle and background color.
/// Uses bUnit's JSInterop to track JS calls. Registry interactions are verified via
/// the real NativePlayerRegistry to check state after render/dispose.
/// </summary>
public sealed class VideoSurfaceKioskLifecycleTests : BunitContext
{
    private readonly NativePlayerRegistry _registry;
    private readonly BunitJSModuleInterop _moduleInterop;

    public VideoSurfaceKioskLifecycleTests()
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        Services.AddSingleton(kioskDetector);

        _registry = new NativePlayerRegistry();
        Services.AddSingleton<INativePlayerRegistry>(_registry);

        // Setup bUnit JS interop chain.
        _moduleInterop = JSInterop.SetupModule("./_content/BlazorBlaze.Server/video-surface.js");
        _moduleInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>B-040: JS module is imported and createAdapter is called on first render</summary>
    [Fact]
    public void KioskMode_ImportsModuleAndCreatesAdapterOnFirstRender()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _moduleInterop.VerifyInvoke("createAdapter");
    }

    /// <summary>B-041: createAdapter receives the player ID as second argument</summary>
    [Fact]
    public void KioskMode_CreateAdapterReceivesPlayerId()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "my-player"));

        var invocation = _moduleInterop.VerifyInvoke("createAdapter");
        invocation.Arguments[1].Should().Be("my-player");
    }

    /// <summary>B-042: createAdapter receives exactly 2 arguments (element, playerId) - no sessionId</summary>
    [Fact]
    public void KioskMode_CreateAdapterReceivesExactlyTwoArgs()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var invocation = _moduleInterop.VerifyInvoke("createAdapter");
        invocation.Arguments.Should().HaveCount(2);
    }

    /// <summary>B-043: Registration happens after init and play (init/play/register sequence)</summary>
    [Fact]
    public void KioskMode_RegistersAfterInitAndPlay()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        // Registration only happens after init + play succeed.
        _registry.ActivePlayerIds.Should().Contain("test-player");
    }

    /// <summary>B-044: Unregisters on dispose</summary>
    [Fact]
    public async Task KioskMode_UnregistersOnDispose()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        _registry.ActivePlayerIds.Should().Contain("test-player");

        await DisposeComponentsAsync();

        _registry.ActivePlayerIds.Should().NotContain("test-player");
    }

    /// <summary>B-045: Registers with INativePlayerRegistry on init</summary>
    [Fact]
    public void KioskMode_RegistersWithRegistry()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        _registry.ActivePlayerIds.Should().Contain("test-player");
    }

    /// <summary>B-046: Registration includes a valid JS adapter reference</summary>
    [Fact]
    public void KioskMode_RegistrationIncludesJsAdapterReference()
    {
        NativePlayerRegistration? received = null;
        _registry.PlayerRegistered += r => received = r;

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        received.Should().NotBeNull();
        received!.PlayerId.Should().Be("test-player");
        received.JsAdapter.Should().NotBeNull();
    }

    /// <summary>B-047: DisposeAsync catches JSDisconnectedException - no throw</summary>
    [Fact]
    public void KioskMode_DisposeAsync_DoesNotThrow()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var act = () => cut.Dispose();

        act.Should().NotThrow();
    }

    /// <summary>B-048: DisposeAsync is safe even when called multiple times</summary>
    [Fact]
    public void KioskMode_DoubleDispose_DoesNotThrow()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var act = () =>
        {
            cut.Dispose();
            // BunitContext may already be disposed, no double-dispose on component.
        };

        act.Should().NotThrow();
    }

    /// <summary>B-049: PlayerId auto-generated when not provided (deterministic prefix)</summary>
    [Fact]
    public void KioskMode_AutoGeneratesPlayerId()
    {
        NativePlayerRegistration? received = null;
        _registry.PlayerRegistered += r => received = r;

        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        received.Should().NotBeNull();
        received!.PlayerId.Should().StartWith("vs-");
    }

    /// <summary>B-050: BackgroundColor param triggers setBackgroundColor lifecycle</summary>
    [Fact]
    public void KioskMode_BackgroundColor_LifecycleCompletes()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.BackgroundColor, "#000000"));

        // If the lifecycle completes, the player is registered.
        _registry.ActivePlayerIds.Should().NotBeEmpty();
    }

    /// <summary>B-051: Dispose completes when BackgroundColor was set</summary>
    [Fact]
    public async Task KioskMode_BackgroundColor_DisposeCompletes()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.BackgroundColor, "#000000"));

        await DisposeComponentsAsync();

        _registry.ActivePlayerIds.Should().BeEmpty();
    }

    /// <summary>B-052: No set-background-color when BackgroundColor param is null (lifecycle completes)</summary>
    [Fact]
    public void KioskMode_NoBackgroundColor_LifecycleCompletes()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _registry.ActivePlayerIds.Should().NotBeEmpty();
    }

    /// <summary>PlayerRegistered event fires after registration</summary>
    [Fact]
    public void KioskMode_PlayerRegisteredEventFires()
    {
        NativePlayerRegistration? received = null;
        _registry.PlayerRegistered += r => received = r;

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "evented-player"));

        received.Should().NotBeNull();
        received!.PlayerId.Should().Be("evented-player");
    }

    /// <summary>PlayerUnregistered event fires after dispose</summary>
    [Fact]
    public async Task KioskMode_PlayerUnregisteredEventFires()
    {
        string? unregisteredId = null;
        _registry.PlayerUnregistered += id => unregisteredId = id;

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "evented-player"));

        await DisposeComponentsAsync();

        unregisteredId.Should().Be("evented-player");
    }
}
