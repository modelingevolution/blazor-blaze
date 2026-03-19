namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// Tracks active VideoSurface instances and provides messaging to native player via a
/// module-level JS function. Scoped per Blazor circuit.
/// </summary>
public interface INativePlayerRegistry : IAsyncDisposable
{
    /// <summary>
    /// Registers a native player instance. Fires <see cref="PlayerRegistered"/> after registration.
    /// </summary>
    void Register(NativePlayerRegistration registration);

    /// <summary>
    /// Unregisters a native player instance. Fires <see cref="PlayerUnregistered"/> after removal.
    /// No-op if the player ID is not found.
    /// </summary>
    void Unregister(string playerId);

    /// <summary>
    /// Sends a message to a specific native player via the module-level postNativeMessage function.
    /// No-op if the player ID is not found.
    /// </summary>
    ValueTask PostMessageAsync(string playerId, object message);

    /// <summary>
    /// Sends a message to all active native players.
    /// </summary>
    ValueTask BroadcastAsync(object message);

    /// <summary>
    /// Returns the IDs of all currently registered players.
    /// Returns a pre-built snapshot; no allocation on access.
    /// </summary>
    IReadOnlyList<string> ActivePlayerIds { get; }

    /// <summary>
    /// Raised synchronously after a player is registered.
    /// </summary>
    event Action<NativePlayerRegistration>? PlayerRegistered;

    /// <summary>
    /// Raised synchronously after a player is unregistered.
    /// </summary>
    event Action<string>? PlayerUnregistered;
}
