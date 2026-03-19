using Microsoft.JSInterop;

namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// Represents a registered native player instance with its JS adapter reference.
/// </summary>
public sealed record NativePlayerRegistration(
    string PlayerId,
    IJSObjectReference JsAdapter);
