using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
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
    internal const string ModulePath = "./_content/BlazorBlaze.Server/video-surface.js";

    private readonly ConcurrentDictionary<string, NativePlayerRegistration> _players = new();
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<NativePlayerRegistry> _logger;
    private IJSObjectReference? _module;
    private string[] _activePlayerIds = [];

    public NativePlayerRegistry(IJSRuntime jsRuntime, ILogger<NativePlayerRegistry> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public IReadOnlyList<string> ActivePlayerIds => Volatile.Read(ref _activePlayerIds);

    public event Action<NativePlayerRegistration>? PlayerRegistered;
    public event Action<NativePlayerRegistration>? PlayerUnregistered;

    public void Register(NativePlayerRegistration registration)
    {
        _players[registration.PlayerId] = registration;
        RebuildActivePlayerIds();
        PlayerRegistered?.Invoke(registration);
    }

    public void Unregister(string playerId)
    {
        if (_players.TryRemove(playerId, out var registration))
        {
            RebuildActivePlayerIds();
            PlayerUnregistered?.Invoke(registration);
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
        catch (JSDisconnectedException ex)
        {
            _logger.LogDebug(ex, "Registry: circuit disconnected during PostMessageAsync for {PlayerId}", playerId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Registry: JS runtime unavailable during PostMessageAsync for {PlayerId}", playerId);
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
        catch (JSDisconnectedException ex)
        {
            _logger.LogDebug(ex, "Registry: circuit disconnected during BroadcastAsync");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Registry: JS runtime unavailable during BroadcastAsync");
        }
    }

    private async ValueTask<IJSObjectReference> EnsureModuleAsync()
    {
        return _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", ModulePath);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException ex)
            {
                _logger.LogDebug(ex, "Registry: circuit disconnected during DisposeAsync");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDebug(ex, "Registry: JS runtime unavailable during DisposeAsync");
            }
        }
    }
}
