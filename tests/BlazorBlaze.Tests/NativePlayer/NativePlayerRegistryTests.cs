using BlazorBlaze.Server.NativePlayer;
using Microsoft.JSInterop;
using NSubstitute;

namespace BlazorBlaze.Tests.NativePlayer;

/// <summary>
/// Tests B-010 through B-017: INativePlayerRegistry behavior.
/// </summary>
public sealed class NativePlayerRegistryTests
{
    private static NativePlayerRegistration CreateRegistration(string playerId)
    {
        var jsAdapter = Substitute.For<IJSObjectReference>();
        return new NativePlayerRegistration(playerId, jsAdapter);
    }

    /// <summary>B-010: Register adds player to ActivePlayerIds</summary>
    [Fact]
    public void Register_AddsPlayerToActivePlayerIds()
    {
        var registry = new NativePlayerRegistry();
        var reg = CreateRegistration("vs-1");

        registry.Register(reg);

        registry.ActivePlayerIds.Should().Contain("vs-1");
    }

    /// <summary>B-011: Unregister removes player</summary>
    [Fact]
    public void Unregister_RemovesPlayerFromActivePlayerIds()
    {
        var registry = new NativePlayerRegistry();
        var reg = CreateRegistration("vs-1");
        registry.Register(reg);

        registry.Unregister("vs-1");

        registry.ActivePlayerIds.Should().NotContain("vs-1");
    }

    /// <summary>B-012: PostMessageAsync sends to specific player only</summary>
    [Fact]
    public async Task PostMessageAsync_SendsToSpecificPlayerOnly()
    {
        var registry = new NativePlayerRegistry();
        var reg1 = CreateRegistration("vs-1");
        var reg2 = CreateRegistration("vs-2");
        registry.Register(reg1);
        registry.Register(reg2);

        var message = new { type = "play", id = "vs-1" };
        await registry.PostMessageAsync("vs-1", message);

        await reg1.JsAdapter.Received(1).InvokeVoidAsync("postMessage", Arg.Any<object[]>());
        await reg2.JsAdapter.DidNotReceive().InvokeVoidAsync("postMessage", Arg.Any<object[]>());
    }

    /// <summary>B-013: BroadcastAsync sends to all players</summary>
    [Fact]
    public async Task BroadcastAsync_SendsToAllPlayers()
    {
        var registry = new NativePlayerRegistry();
        var reg1 = CreateRegistration("vs-1");
        var reg2 = CreateRegistration("vs-2");
        registry.Register(reg1);
        registry.Register(reg2);

        var message = new { type = "set-overlay", name = "segmentation", visible = true };
        await registry.BroadcastAsync(message);

        await reg1.JsAdapter.Received(1).InvokeVoidAsync("postMessage", Arg.Any<object[]>());
        await reg2.JsAdapter.Received(1).InvokeVoidAsync("postMessage", Arg.Any<object[]>());
    }

    /// <summary>B-014: PostMessageAsync to unknown player - no exception</summary>
    [Fact]
    public async Task PostMessageAsync_UnknownPlayerId_DoesNotThrow()
    {
        var registry = new NativePlayerRegistry();

        var act = () => registry.PostMessageAsync("nonexistent", new { type = "play" }).AsTask();

        await act.Should().NotThrowAsync();
    }

    /// <summary>B-015: Duplicate register with same ID replaces previous</summary>
    [Fact]
    public async Task Register_DuplicateId_ReplacesPrevious()
    {
        var registry = new NativePlayerRegistry();
        var reg1 = CreateRegistration("vs-1");
        var reg2 = CreateRegistration("vs-1");
        registry.Register(reg1);
        registry.Register(reg2);

        var message = new { type = "play", id = "vs-1" };
        await registry.PostMessageAsync("vs-1", message);

        // Only the second adapter should receive the message.
        await reg2.JsAdapter.Received(1).InvokeVoidAsync("postMessage", Arg.Any<object[]>());
        await reg1.JsAdapter.DidNotReceive().InvokeVoidAsync("postMessage", Arg.Any<object[]>());
    }

    /// <summary>B-016: Unregister unknown ID - no exception</summary>
    [Fact]
    public void Unregister_UnknownId_DoesNotThrow()
    {
        var registry = new NativePlayerRegistry();

        var act = () => registry.Unregister("nonexistent");

        act.Should().NotThrow();
    }

    /// <summary>B-017: Concurrent register/unregister - thread-safe, consistent state</summary>
    [Fact]
    public async Task ConcurrentRegisterUnregister_ThreadSafe()
    {
        var registry = new NativePlayerRegistry();
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
        var registry = new NativePlayerRegistry();
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
        var registry = new NativePlayerRegistry();
        var reg = CreateRegistration("vs-1");
        registry.Register(reg);
        string? removedId = null;
        registry.PlayerUnregistered += id => removedId = id;

        registry.Unregister("vs-1");

        removedId.Should().Be("vs-1");
    }

    /// <summary>PostMessageAsync catches JSDisconnectedException</summary>
    [Fact]
    public async Task PostMessageAsync_CatchesJSDisconnectedException()
    {
        var registry = new NativePlayerRegistry();
        var reg = CreateRegistration("vs-1");
        reg.JsAdapter
            .When(x => x.InvokeVoidAsync("postMessage", Arg.Any<object[]>()))
            .Do(_ => throw new JSDisconnectedException("disconnected"));
        registry.Register(reg);

        var act = () => registry.PostMessageAsync("vs-1", new { type = "play" }).AsTask();

        await act.Should().NotThrowAsync();
    }

    /// <summary>PostMessageAsync catches InvalidOperationException</summary>
    [Fact]
    public async Task PostMessageAsync_CatchesInvalidOperationException()
    {
        var registry = new NativePlayerRegistry();
        var reg = CreateRegistration("vs-1");
        reg.JsAdapter
            .When(x => x.InvokeVoidAsync("postMessage", Arg.Any<object[]>()))
            .Do(_ => throw new InvalidOperationException("runtime unavailable"));
        registry.Register(reg);

        var act = () => registry.PostMessageAsync("vs-1", new { type = "play" }).AsTask();

        await act.Should().NotThrowAsync();
    }
}
