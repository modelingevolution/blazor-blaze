namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// Configuration options for kiosk detection via User-Agent header.
/// </summary>
public sealed class KioskDetectorOptions
{
    /// <summary>
    /// The token to search for in the User-Agent header (case-insensitive).
    /// Default: "RocketWelder-Kiosk".
    /// </summary>
    public string UserAgentToken { get; set; } = "RocketWelder-Kiosk";
}
