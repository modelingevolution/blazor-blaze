using BlazorBlaze.Server.NativePlayer;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace BlazorBlaze.Server.Tests.NativePlayer;

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

    [Fact]
    public void IsNativePlayer_ReturnsTrue_WhenUserAgentContainsToken()
    {
        var detector = CreateDetector("Mozilla/5.0 RocketWelder-Kiosk/1.0");

        detector.IsNativePlayer.Should().BeTrue();
    }

    [Fact]
    public void IsNativePlayer_ReturnsFalse_WhenUserAgentDoesNotContainToken()
    {
        var detector = CreateDetector("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        detector.IsNativePlayer.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsNativePlayer_ReturnsFalse_WhenUserAgentIsNullOrEmpty(string? userAgent)
    {
        var detector = CreateDetector(userAgent);

        detector.IsNativePlayer.Should().BeFalse();
    }

    [Theory]
    [InlineData("Mozilla/5.0 ROCKETWELDER-KIOSK/1.0")]
    [InlineData("Mozilla/5.0 rocketwelder-kiosk/1.0")]
    [InlineData("Mozilla/5.0 RocketWelder-kiosk/1.0")]
    public void IsNativePlayer_IsCaseInsensitive(string userAgent)
    {
        var detector = CreateDetector(userAgent);

        detector.IsNativePlayer.Should().BeTrue();
    }

    [Fact]
    public void IsNativePlayer_IsSynchronousProperty()
    {
        var detector = CreateDetector("Mozilla/5.0 RocketWelder-Kiosk/1.0");

        var result1 = detector.IsNativePlayer;
        var result2 = detector.IsNativePlayer;

        result1.Should().Be(result2);
        result1.Should().BeTrue();
    }

    [Fact]
    public void IsNativePlayer_ValueIsCached_DoesNotReReadHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 RocketWelder-Kiosk/1.0";

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var detector = new KioskDetector(accessor);

        detector.IsNativePlayer.Should().BeTrue();

        accessor.HttpContext.Returns((HttpContext?)null);
        detector.IsNativePlayer.Should().BeTrue();
    }
}
