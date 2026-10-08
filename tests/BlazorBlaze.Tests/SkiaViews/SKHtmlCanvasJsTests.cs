using System.ComponentModel;
using System.Diagnostics;

namespace BlazorBlaze.Tests.SkiaViews;

public sealed class SKHtmlCanvasJsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task SKHtmlCanvasJs_NodeTestSuite_Passes()
    {
        var script = Path.Combine(RepositoryRoot(), "tests", "BlazorBlaze.Tests", "SkiaViews", "js", "SKHtmlCanvas.test.mjs");
        var start = new ProcessStartInfo("node", ["--test", script])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var node = StartNode(start);
        var output = node.StandardOutput.ReadToEndAsync();
        var error = node.StandardError.ReadToEndAsync();
        var finished = node.WaitForExit(Timeout);
        if (!finished)
            node.Kill(entireProcessTree: true);
        var log = await output + await error;

        finished.Should().BeTrue($"node --test must finish within {Timeout.TotalSeconds} s.{Environment.NewLine}{log}");
        node.ExitCode.Should().Be(0, log);
    }

    private static Process StartNode(ProcessStartInfo start)
    {
        try
        {
            return Process.Start(start)!;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Node.js is required to run the SKHtmlCanvas.js tests.", ex);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlazorBlaze.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("BlazorBlaze.sln not found above the test output.");
    }
}
