namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// Detects whether the current request originates from a native player's embedded browser.
/// Synchronous — no async, no JS interop. Available from the first render.
/// </summary>
public interface IKioskDetector
{
    /// <summary>
    /// True when User-Agent contains the kiosk token.
    /// </summary>
    bool IsKiosk { get; }
}
