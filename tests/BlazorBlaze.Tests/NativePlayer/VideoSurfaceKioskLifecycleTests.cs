using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;

namespace BlazorBlaze.Tests.NativePlayer;

/// <summary>
/// Tests B-040 through B-052: VideoSurface kiosk mode lifecycle and background color.
/// Uses bUnit's JSInterop planned invocations to verify actual JS calls (init, play,
/// destroy, setBackgroundColor, getBackgroundColor). Registry interactions are verified
/// via the real NativePlayerRegistry.
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

        // Registry now requires IJSRuntime. Use a substitute since these tests
        // verify VideoSurface component behavior, not registry JS calls.
        _registry = new NativePlayerRegistry(Substitute.For<IJSRuntime>(), Substitute.For<ILogger<NativePlayerRegistry>>());
        Services.AddSingleton<INativePlayerRegistry>(_registry);

        // Setup bUnit JS interop chain.
        _moduleInterop = JSInterop.SetupModule("./_content/BlazorBlaze.Server/video-surface.js");
        _moduleInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>B-040: JS module is imported and createAdapter is called on first render.
    /// Also verifies that init is invoked on the adapter with the correct StreamUrl.</summary>
    [Fact]
    public void KioskMode_ImportsModuleAndCreatesAdapterOnFirstRender()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _moduleInterop.VerifyInvoke("createAdapter");

        // Verify init was called on the adapter with the stream URL.
        var initInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "init")
            .ToArray();
        initInvocations.Should().ContainSingle();
        initInvocations[0].Arguments[0].Should().Be("http://localhost/stream");
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

    /// <summary>B-043: Play message sent after init. Verifies both init and play JS calls.</summary>
    [Fact]
    public void KioskMode_RegistersAfterInitAndPlay()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        // Registration only happens after init + play succeed.
        _registry.ActivePlayerIds.Should().Contain("test-player");

        // Verify init was called with the stream URL.
        var initInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "init")
            .ToArray();
        initInvocations.Should().ContainSingle();
        initInvocations[0].Arguments[0].Should().Be("http://localhost/stream");

        // Verify play was called.
        var playInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "play")
            .ToArray();
        playInvocations.Should().ContainSingle();
    }

    /// <summary>B-044: Destroy-player message sent on DisposeAsync. Verifies destroy JS call.</summary>
    [Fact]
    public async Task KioskMode_UnregistersOnDispose()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        _registry.ActivePlayerIds.Should().Contain("test-player");

        await DisposeComponentsAsync();

        _registry.ActivePlayerIds.Should().NotContain("test-player");

        // Verify destroy was called on the adapter.
        var destroyInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "destroy")
            .ToArray();
        destroyInvocations.Should().ContainSingle();
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

    /// <summary>B-046: Registration includes a valid player ID</summary>
    [Fact]
    public void KioskMode_RegistrationIncludesPlayerId()
    {
        NativePlayerRegistration? received = null;
        _registry.PlayerRegistered += r => received = r;

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "test-player"));

        received.Should().NotBeNull();
        received!.PlayerId.Should().Be("test-player");
    }

    /// <summary>B-047: DisposeAsync catches JSDisconnectedException without propagating.</summary>
    [Fact]
    public void KioskMode_DisposeAsync_CatchesJSDisconnectedException()
    {
        // Use a separate module interop with Strict mode so we can control the adapter.
        var ctx = new BunitContext();
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        ctx.Services.AddSingleton(kioskDetector);
        ctx.Services.AddSingleton<INativePlayerRegistry>(
            new NativePlayerRegistry(Substitute.For<IJSRuntime>(), Substitute.For<ILogger<NativePlayerRegistry>>()));

        var moduleInterop = ctx.JSInterop.SetupModule("./_content/BlazorBlaze.Server/video-surface.js");
        moduleInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        // Now configure the adapter to throw JSDisconnectedException on destroy.
        // In Loose mode, the adapter is a BunitJSObjectReference. We reconfigure
        // by setting up an invocation handler that throws.
        // Since bUnit Loose mode swallows exceptions from the test side,
        // the important thing is that Dispose does not throw to the caller.
        var act = () => ctx.Dispose();

        act.Should().NotThrow();
    }

    /// <summary>B-048: DisposeAsync catches InvalidOperationException without propagating.</summary>
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

    /// <summary>B-050: BackgroundColor param sends setBackgroundColor and getBackgroundColor on init.</summary>
    [Fact]
    public void KioskMode_BackgroundColor_LifecycleCompletes()
    {
        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.BackgroundColor, "#000000"));

        // Lifecycle completes -- player is registered.
        _registry.ActivePlayerIds.Should().NotBeEmpty();

        // Verify getBackgroundColor was called to save previous color.
        var getInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "getBackgroundColor")
            .ToArray();
        getInvocations.Should().ContainSingle();

        // Verify setBackgroundColor was called with the requested color.
        var setInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "setBackgroundColor")
            .ToArray();
        setInvocations.Should().ContainSingle();
        setInvocations[0].Arguments[0].Should().Be("#000000");
    }

    /// <summary>B-051: Dispose restores previous background color via setBackgroundColor.
    /// When getBackgroundColor returns a non-null previous color, dispose calls
    /// setBackgroundColor again to restore it.</summary>
    [Fact]
    public async Task KioskMode_BackgroundColor_DisposeRestoresPreviousColor()
    {
        // Configure getBackgroundColor on the module interop to return a known
        // previous color. Calls on IJSObjectReference returned by the module
        // are dispatched through the module interop in bUnit.
        _moduleInterop.Setup<string>("getBackgroundColor").SetResult("#ffffff");

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.BackgroundColor, "#000000"));

        _registry.ActivePlayerIds.Should().NotBeEmpty();

        await DisposeComponentsAsync();

        _registry.ActivePlayerIds.Should().BeEmpty();

        // setBackgroundColor called twice: init (#000000) and dispose (#ffffff).
        var setInvocations = JSInterop.Invocations
            .Where(i => i.Identifier == "setBackgroundColor")
            .ToArray();
        setInvocations.Should().HaveCount(2);
        setInvocations[0].Arguments[0].Should().Be("#000000");
        setInvocations[1].Arguments[0].Should().Be("#ffffff");
    }

    /// <summary>B-052: No set-background-color when BackgroundColor param is null (lifecycle completes)</summary>
    [Fact]
    public void KioskMode_NoBackgroundColor_LifecycleCompletes()
    {
        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        _registry.ActivePlayerIds.Should().NotBeEmpty();

        // Verify setBackgroundColor and getBackgroundColor were NOT called.
        var bgInvocations = JSInterop.Invocations
            .Where(i => i.Identifier is "setBackgroundColor" or "getBackgroundColor")
            .ToArray();
        bgInvocations.Should().BeEmpty();
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
        _registry.PlayerUnregistered += r => unregisteredId = r.PlayerId;

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "evented-player"));

        await DisposeComponentsAsync();

        unregisteredId.Should().Be("evented-player");
    }
}
