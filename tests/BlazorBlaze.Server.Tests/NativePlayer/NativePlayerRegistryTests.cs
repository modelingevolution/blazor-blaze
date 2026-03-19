using BlazorBlaze.Server.NativePlayer;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;

namespace BlazorBlaze.Server.Tests.NativePlayer;

/// <summary>
/// Tests B-010 through B-017: INativePlayerRegistry behavior.
/// The registry now uses a module-level postNativeMessage function instead of
/// per-adapter IJSObjectReference calls.
/// </summary>
public sealed class NativePlayerRegistryTests
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IJSObjectReference _jsModule;

    public NativePlayerRegistryTests()
    {
        _jsRuntime = Substitute.For<IJSRuntime>();
        _jsModule = Substitute.For<IJSObjectReference>();

        // When the registry lazily imports the module, return our mock.
        _jsRuntime
            .InvokeAsync<IJSObjectReference>("import", Arg.Any<object[]>())
            .Returns(ValueTask.FromResult(_jsModule));
    }

    private NativePlayerRegistry CreateRegistry() => new(_jsRuntime, Substitute.For<ILogger<NativePlayerRegistry>>());

    private static NativePlayerRegistration CreateRegistration(string playerId)
        => new(playerId);

    /// <summary>B-010: Register adds player to ActivePlayerIds</summary>
    [Fact]
    public void Register_AddsPlayerToActivePlayerIds()
    {
        var registry = CreateRegistry();
        var reg = CreateRegistration("vs-1");

        registry.Register(reg);

        registry.ActivePlayerIds.Should().Contain("vs-1");
    }

    /// <summary>B-011: Unregister removes player</summary>
    [Fact]
    public void Unregister_RemovesPlayerFromActivePlayerIds()
    {
        var registry = CreateRegistry();
        var reg = CreateRegistration("vs-1");
        registry.Register(reg);

        registry.Unregister("vs-1");

        registry.ActivePlayerIds.Should().NotContain("vs-1");
    }

    /// <summary>B-012: PostMessageAsync calls module-level postNativeMessage for registered player</summary>
    [Fact]
    public async Task PostMessageAsync_CallsModuleLevelFunction()
    {
        var registry = CreateRegistry();
        registry.Register(CreateRegistration("vs-1"));
        registry.Register(CreateRegistration("vs-2"));

        var message = new { type = "play", id = "vs-1" };
        await registry.PostMessageAsync("vs-1", message);

        await _jsModule.Received(1).InvokeVoidAsync("postNativeMessage", Arg.Any<object[]>());
    }

    /// <summary>B-013: BroadcastAsync calls module-level postNativeMessage</summary>
    [Fact]
    public async Task BroadcastAsync_CallsModuleLevelFunction()
    {
        var registry = CreateRegistry();
        registry.Register(CreateRegistration("vs-1"));
        registry.Register(CreateRegistration("vs-2"));

        var message = new { type = "set-overlay", name = "segmentation", visible = true };
        await registry.BroadcastAsync(message);

        await _jsModule.Received(1).InvokeVoidAsync("postNativeMessage", Arg.Any<object[]>());
    }

    /// <summary>B-014: PostMessageAsync to unknown player - no exception, no JS call</summary>
    [Fact]
    public async Task PostMessageAsync_UnknownPlayerId_DoesNotThrow()
    {
        var registry = CreateRegistry();

        var act = () => registry.PostMessageAsync("nonexistent", new { type = "play" }).AsTask();

        await act.Should().NotThrowAsync();
        await _jsModule.DidNotReceive().InvokeVoidAsync("postNativeMessage", Arg.Any<object[]>());
    }

    /// <summary>B-015: Duplicate register with same ID replaces previous</summary>
    [Fact]
    public void Register_DuplicateId_ReplacesPrevious()
    {
        var registry = CreateRegistry();
        var reg1 = CreateRegistration("vs-1");
        var reg2 = CreateRegistration("vs-1");
        registry.Register(reg1);
        registry.Register(reg2);

        // Only one entry should exist.
        registry.ActivePlayerIds.Should().HaveCount(1);
        registry.ActivePlayerIds.Should().Contain("vs-1");
    }

    /// <summary>B-016: Unregister unknown ID - no exception</summary>
    [Fact]
    public void Unregister_UnknownId_DoesNotThrow()
    {
        var registry = CreateRegistry();

        var act = () => registry.Unregister("nonexistent");

        act.Should().NotThrow();
    }

    /// <summary>B-017: Concurrent register/unregister - thread-safe, consistent state</summary>
    [Fact]
    public async Task ConcurrentRegisterUnregister_ThreadSafe()
    {
        var registry = CreateRegistry();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var id = $"vs-{i}";
            tasks.Add(Task.Run(() =>
            {
                var reg = CreateRegistration(id);
                registry.Register(reg);
            }));
        }

        await Task.WhenAll(tasks);
        registry.ActivePlayerIds.Should().HaveCount(100);

        tasks.Clear();
        for (int i = 0; i < 100; i++)
        {
            var id = $"vs-{i}";
            tasks.Add(Task.Run(() => registry.Unregister(id)));
        }

        await Task.WhenAll(tasks);
        registry.ActivePlayerIds.Should().BeEmpty();
    }

    /// <summary>PlayerRegistered event fires after register</summary>
    [Fact]
    public void Register_FiresPlayerRegisteredEvent()
    {
        var registry = CreateRegistry();
        var reg = CreateRegistration("vs-1");
        NativePlayerRegistration? received = null;
        registry.PlayerRegistered += r => received = r;

        registry.Register(reg);

        received.Should().NotBeNull();
        received!.PlayerId.Should().Be("vs-1");
    }

    /// <summary>PlayerUnregistered event fires after unregister</summary>
    [Fact]
    public void Unregister_FiresPlayerUnregisteredEvent()
    {
        var registry = CreateRegistry();
        var reg = CreateRegistration("vs-1");
        registry.Register(reg);
        NativePlayerRegistration? removed = null;
        registry.PlayerUnregistered += r => removed = r;

        registry.Unregister("vs-1");

        removed.Should().NotBeNull();
        removed!.PlayerId.Should().Be("vs-1");
    }

    /// <summary>PostMessageAsync catches JSDisconnectedException from module call</summary>
    [Fact]
    public async Task PostMessageAsync_CatchesJSDisconnectedException()
    {
        var registry = CreateRegistry();
        registry.Register(CreateRegistration("vs-1"));

        _jsModule
            .When(x => x.InvokeVoidAsync("postNativeMessage", Arg.Any<object[]>()))
            .Do(_ => throw new JSDisconnectedException("disconnected"));

        var act = () => registry.PostMessageAsync("vs-1", new { type = "play" }).AsTask();

        await act.Should().NotThrowAsync();
    }

    /// <summary>PostMessageAsync catches InvalidOperationException from module call</summary>
    [Fact]
    public async Task PostMessageAsync_CatchesInvalidOperationException()
    {
        var registry = CreateRegistry();
        registry.Register(CreateRegistration("vs-1"));

        _jsModule
            .When(x => x.InvokeVoidAsync("postNativeMessage", Arg.Any<object[]>()))
            .Do(_ => throw new InvalidOperationException("runtime unavailable"));

        var act = () => registry.PostMessageAsync("vs-1", new { type = "play" }).AsTask();

        await act.Should().NotThrowAsync();
    }

    /// <summary>BroadcastAsync skips JS call when no players registered</summary>
    [Fact]
    public async Task BroadcastAsync_NoPlayers_DoesNotCallJs()
    {
        var registry = CreateRegistry();

        await registry.BroadcastAsync(new { type = "test" });

        await _jsModule.DidNotReceive().InvokeVoidAsync("postNativeMessage", Arg.Any<object[]>());
    }

    /// <summary>DisposeAsync disposes the JS module reference</summary>
    [Fact]
    public async Task DisposeAsync_DisposesModuleReference()
    {
        var registry = CreateRegistry();
        registry.Register(CreateRegistration("vs-1"));

        // Trigger module import by calling PostMessageAsync.
        await registry.PostMessageAsync("vs-1", new { type = "play" });

        await registry.DisposeAsync();

        await _jsModule.Received(1).DisposeAsync();
    }
}
