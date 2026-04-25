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
    public void IsKiosk_ReturnsTrue_WhenUserAgentContainsToken()
    {
        var detector = CreateDetector("Mozilla/5.0 RocketWelder-Kiosk/1.0");

        detector.IsKiosk.Should().BeTrue();
    }

    [Fact]
    public void IsKiosk_ReturnsFalse_WhenUserAgentDoesNotContainToken()
    {
        var detector = CreateDetector("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        detector.IsKiosk.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsKiosk_ReturnsFalse_WhenUserAgentIsNullOrEmpty(string? userAgent)
    {
        var detector = CreateDetector(userAgent);

        detector.IsKiosk.Should().BeFalse();
    }

    [Theory]
    [InlineData("Mozilla/5.0 ROCKETWELDER-KIOSK/1.0")]
    [InlineData("Mozilla/5.0 rocketwelder-kiosk/1.0")]
    [InlineData("Mozilla/5.0 RocketWelder-kiosk/1.0")]
    public void IsKiosk_IsCaseInsensitive(string userAgent)
    {
        var detector = CreateDetector(userAgent);

        detector.IsKiosk.Should().BeTrue();
    }

    [Fact]
    public void IsKiosk_IsSynchronousProperty()
    {
        var detector = CreateDetector("Mozilla/5.0 RocketWelder-Kiosk/1.0");

        var result1 = detector.IsKiosk;
        var result2 = detector.IsKiosk;

        result1.Should().Be(result2);
        result1.Should().BeTrue();
    }

    [Fact]
    public void IsKiosk_ValueIsCached_DoesNotReReadHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 RocketWelder-Kiosk/1.0";

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var detector = new KioskDetector(accessor);

        detector.IsKiosk.Should().BeTrue();

        accessor.HttpContext.Returns((HttpContext?)null);
        detector.IsKiosk.Should().BeTrue();
    }
}
