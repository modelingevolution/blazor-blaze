using System.Collections.Concurrent;
using Microsoft.JSInterop;

namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// Scoped registry of active native player instances.
/// Thread-safe via ConcurrentDictionary. ActivePlayerIds is a pre-built
/// snapshot (string[]) swapped atomically on Register/Unregister to avoid
/// per-access allocation.
/// </summary>
public sealed class NativePlayerRegistry : INativePlayerRegistry
{
    private readonly ConcurrentDictionary<string, NativePlayerRegistration> _players = new();
    private string[] _activePlayerIds = [];

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
        if (!_players.TryGetValue(playerId, out var registration))
            return;

        try
        {
            await registration.JsAdapter.InvokeVoidAsync("postMessage", message);
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
        foreach (var registration in _players.Values)
        {
            try
            {
                await registration.JsAdapter.InvokeVoidAsync("postMessage", message);
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
