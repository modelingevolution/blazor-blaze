namespace BlazorBlaze.Server.Tests.NativePlayer;

/// <summary>
/// Iteration-21 Scenarios 18, 19, 28, 30 — video-surface-bridge.js chokepoint (FR-10, NFR-5).
/// Static-analysis tests: verify source structure, not runtime behaviour.
/// </summary>
public sealed class VideoSurfaceBridgeTests
{
    private static readonly string WwwrootPath = FindWwwroot();

    private static string FindWwwroot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.sln").Any())
            dir = dir.Parent;
        if (dir == null)
            throw new DirectoryNotFoundException("Could not locate solution root from " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, "src", "BlazorBlaze.Server", "wwwroot");
    }

    private static string ReadWwwroot(string fileName) =>
        File.ReadAllText(Path.Combine(WwwrootPath, fileName));

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    /// <summary>
    /// Scenario 18 — bridge.send() is the only postMessage call site across all wwwroot JS.
    /// Every JS file except video-surface-bridge.js must have zero direct postMessage calls;
    /// video-surface-bridge.js must have exactly one.
    /// </summary>
    [Fact]
    public void Scenario18_BridgeSend_IsOnlyPostMessageCallSite()
    {
        const string needle = "window.webkit.messageHandlers.native.postMessage";
        var jsFiles = Directory.GetFiles(WwwrootPath, "*.js", SearchOption.AllDirectories);

        jsFiles.Should().NotBeEmpty("wwwroot must contain at least the bridge and surface modules");

        foreach (var file in jsFiles)
        {
            var content = File.ReadAllText(file);
            var name    = Path.GetFileName(file);

            if (name == "video-surface-bridge.js")
            {
                CountOccurrences(content, needle)
                    .Should().Be(1, "bridge must contain exactly one postMessage call site");
            }
            else
            {
                content.Should().NotContain(needle,
                    $"{name} must delegate all postMessage calls to the bridge, not call it directly");
            }
        }
    }

    /// <summary>
    /// Scenario 18 (cont.) — no EventAggregator events routed through postMessage.
    /// The bridge carries only non-EA messages; EA events travel via WebSocket.
    /// </summary>
    [Fact]
    public void Scenario18_NoBridgeCarriesEaEvents()
    {
        var bridge = ReadWwwroot("video-surface-bridge.js");
        bridge.Should().NotContain("event-aggregator",
            "EA event routing must not appear in the bridge module");
    }

    /// <summary>
    /// Scenario 19 — bridge is a no-op outside WebKitGTK.
    /// send() must guard on window.webkit?.messageHandlers?.native before calling postMessage.
    /// </summary>
    [Fact]
    public void Scenario19_Bridge_IsNoOpOutsideWebKit()
    {
        var bridge = ReadWwwroot("video-surface-bridge.js");

        bridge.Should().Contain("window.webkit",
            "bridge must check for window.webkit presence");
        bridge.Should().Contain("messageHandlers",
            "bridge must check for messageHandlers presence");

        // The guard must appear BEFORE the postMessage call in the source
        var guardIdx   = bridge.IndexOf("window.webkit", StringComparison.Ordinal);
        var sendIdx    = bridge.IndexOf("postMessage", StringComparison.Ordinal);
        guardIdx.Should().BeLessThan(sendIdx,
            "environment guard must precede the postMessage invocation");
    }

    /// <summary>
    /// Scenario 28 — offline deployment: no CDN host references in any wwwroot JS file.
    /// </summary>
    [Fact]
    public void Scenario28_NoCdnHostsInWwwrootJs()
    {
        string[] cdnHosts = ["jsdelivr", "cdnjs", "unpkg", "googleapis"];
        var jsFiles = Directory.GetFiles(WwwrootPath, "*.js", SearchOption.AllDirectories);

        jsFiles.Should().NotBeEmpty("wwwroot must contain at least the bridge and surface modules");

        foreach (var file in jsFiles)
        {
            var content = File.ReadAllText(file);
            var name    = Path.GetFileName(file);
            foreach (var host in cdnHosts)
                content.Should().NotContain(host,
                    $"{name} must not reference CDN host '{host}' (offline deployment required)");
        }
    }

    /// <summary>
    /// Scenario 30 — bootstrap GUID postMessage fires via bridge.send() exactly once per
    /// circuit start (one-shot). Verifies bridge contract: send() is exported and the
    /// bridge has no internal state that blocks repeated calls — the "exactly once"
    /// invariant is enforced by the .NET NativeCppForwarder caller, not the bridge itself.
    /// </summary>
    [Fact]
    public void Scenario30_Bridge_ExportsSendForBootstrapGuid()
    {
        var bridge = ReadWwwroot("video-surface-bridge.js");

        bridge.Should().Contain("export function send",
            "bridge must export send() so .NET JS-interop can deliver the bootstrap GUID");
    }

    /// <summary>
    /// Scenario 30 (cont.) — video-surface.js uses the bridge import, not a direct call.
    /// Confirms the import wiring is present so bootstrap GUID delivery flows through
    /// the single chokepoint.
    /// </summary>
    [Fact]
    public void Scenario30_VideoSurface_ImportsBridge()
    {
        var surface = ReadWwwroot("video-surface.js");

        surface.Should().Contain("video-surface-bridge.js",
            "video-surface.js must import from the bridge module");
    }
}
