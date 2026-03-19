using Microsoft.AspNetCore.Http;

namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// Detects kiosk mode by checking the User-Agent header for a hardcoded token.
/// The token is baked into the C++ native player and cannot be configured.
/// Scoped service — reads UA once in the constructor and caches the result.
/// </summary>
public sealed class KioskDetector : IKioskDetector
{
    private const string KioskUserAgentToken = "RocketWelder-Kiosk";

    /// <summary>
    /// Cached kiosk detection result for this scope/circuit.
    /// </summary>
    public bool IsKiosk { get; }

    public KioskDetector(IHttpContextAccessor httpContextAccessor)
    {
        var userAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

        IsKiosk = !string.IsNullOrEmpty(userAgent)
                  && userAgent.Contains(KioskUserAgentToken, StringComparison.OrdinalIgnoreCase);
    }
}
