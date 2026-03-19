using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// Detects kiosk mode by checking the User-Agent header for a configurable token.
/// Scoped service — reads UA once in the constructor and caches the result.
/// </summary>
public sealed class KioskDetector : IKioskDetector
{
    /// <summary>
    /// Cached kiosk detection result for this scope/circuit.
    /// </summary>
    public bool IsKiosk { get; }

    public KioskDetector(IHttpContextAccessor httpContextAccessor, IOptions<KioskDetectorOptions> options)
    {
        var userAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
        var token = options.Value.UserAgentToken;

        IsKiosk = !string.IsNullOrEmpty(userAgent)
                  && userAgent.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
