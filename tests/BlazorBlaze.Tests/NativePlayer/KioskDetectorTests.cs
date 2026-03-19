using BlazorBlaze.Server.NativePlayer;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace BlazorBlaze.Tests.NativePlayer;

/// <summary>
/// Tests B-001 through B-007: IKioskDetector behavior.
/// </summary>
public sealed class KioskDetectorTests
{
    private static KioskDetector CreateDetector(string? userAgent)
    {
        var httpContext = new DefaultHttpContext();
        if (userAgent is not null)
            httpContext.Request.Headers["User-Agent"] = userAgent;

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        return new KioskDetector(accessor);
    }

    /// <summary>B-001: UA contains token -> IsKiosk == true</summary>
    [Fact]
    public void IsKiosk_ReturnsTrue_WhenUserAgentContainsToken()
    {
        var detector = CreateDetector("Mozilla/5.0 RocketWelder-Kiosk/1.0");

        detector.IsKiosk.Should().BeTrue();
    }

    /// <summary>B-002: Normal UA -> IsKiosk == false</summary>
    [Fact]
    public void IsKiosk_ReturnsFalse_WhenUserAgentDoesNotContainToken()
    {
        var detector = CreateDetector("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        detector.IsKiosk.Should().BeFalse();
    }

    /// <summary>B-003: Empty/null UA -> IsKiosk == false</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsKiosk_ReturnsFalse_WhenUserAgentIsNullOrEmpty(string? userAgent)
    {
        var detector = CreateDetector(userAgent);

        detector.IsKiosk.Should().BeFalse();
    }

    /// <summary>B-004: Token matching is case-insensitive</summary>
    [Theory]
    [InlineData("Mozilla/5.0 ROCKETWELDER-KIOSK/1.0")]
    [InlineData("Mozilla/5.0 rocketwelder-kiosk/1.0")]
    [InlineData("Mozilla/5.0 RocketWelder-kiosk/1.0")]
    public void IsKiosk_IsCaseInsensitive(string userAgent)
    {
        var detector = CreateDetector(userAgent);

        detector.IsKiosk.Should().BeTrue();
    }

    /// <summary>B-006: Detection is synchronous (property, no async)</summary>
    [Fact]
    public void IsKiosk_IsSynchronousProperty()
    {
        var detector = CreateDetector("Mozilla/5.0 RocketWelder-Kiosk/1.0");

        // Property access is synchronous — no Task/ValueTask return type.
        // Accessing it multiple times returns the same value instantly.
        var result1 = detector.IsKiosk;
        var result2 = detector.IsKiosk;

        result1.Should().Be(result2);
        result1.Should().BeTrue();
    }

    /// <summary>B-007: Value persists for Blazor circuit lifetime (cached in constructor)</summary>
    [Fact]
    public void IsKiosk_ValueIsCached_DoesNotReReadHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 RocketWelder-Kiosk/1.0";

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var detector = new KioskDetector(accessor);

        detector.IsKiosk.Should().BeTrue();

        // Even if the underlying HttpContext changes, the cached value persists.
        accessor.HttpContext.Returns((HttpContext?)null);
        detector.IsKiosk.Should().BeTrue();
    }
}
