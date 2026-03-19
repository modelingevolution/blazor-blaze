using System.Collections.Concurrent;
using Microsoft.JSInterop;

namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// Scoped registry of active native player instances.
/// Thread-safe via ConcurrentDictionary. ActivePlayerIds is a pre-built
/// snapshot (string[]) swapped atomically on Register/Unregister to avoid
/// per-access allocation.
/// Uses a single lazily-imported JS module for posting messages to the native
/// layer instead of per-adapter IJSObjectReference calls.
/// </summary>
public sealed class NativePlayerRegistry : INativePlayerRegistry
{
    private readonly ConcurrentDictionary<string, NativePlayerRegistration> _players = new();
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private string[] _activePlayerIds = [];

    public NativePlayerRegistry(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public IReadOnlyList<string> ActivePlayerIds => Volatile.Read(ref _activePlayerIds);

    public event Action<NativePlayerRegistration>? PlayerRegistered;
    public event Action<string>? PlayerUnregistered;

    public void Register(NativePlayerRegistration registration)
    {
        _players[registration.PlayerId] = registration;
        RebuildActivePlayerIds();
        PlayerRegistered?.Invoke(registration);
    }

    public void Unregister(string playerId)
    {
        if (_players.TryRemove(playerId, out _))
        {
            RebuildActivePlayerIds();
            PlayerUnregistered?.Invoke(playerId);
        }
    }

    private void RebuildActivePlayerIds()
    {
        Volatile.Write(ref _activePlayerIds, [.. _players.Keys]);
    }

    public async ValueTask PostMessageAsync(string playerId, object message)
    {
        if (!_players.ContainsKey(playerId))
            return;

        try
        {
            var module = await EnsureModuleAsync();
            await module.InvokeVoidAsync("postNativeMessage", message);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — silently ignore.
        }
        catch (InvalidOperationException)
        {
            // JS runtime unavailable — silently ignore.
        }
    }

    public async ValueTask BroadcastAsync(object message)
    {
        if (_players.IsEmpty)
            return;

        try
        {
            var module = await EnsureModuleAsync();
            await module.InvokeVoidAsync("postNativeMessage", message);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — silently ignore.
        }
        catch (InvalidOperationException)
        {
            // JS runtime unavailable — silently ignore.
        }
    }

    private async ValueTask<IJSObjectReference> EnsureModuleAsync()
    {
        return _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBlaze.Server/video-surface.js");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected — silently ignore.
            }
            catch (InvalidOperationException)
            {
                // JS runtime unavailable — silently ignore.
            }
        }
    }
}
