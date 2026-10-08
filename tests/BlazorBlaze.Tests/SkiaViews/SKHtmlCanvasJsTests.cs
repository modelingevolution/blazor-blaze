using System.Diagnostics;

namespace BlazorBlaze.Tests.SkiaViews;

public sealed class SKHtmlCanvasJsTests
{
    [Fact]
    public void SKHtmlCanvasJs_NodeTestSuite_Passes()
    {
        var script = Path.Combine(RepositoryRoot(), "tests", "BlazorBlaze.Tests", "SkiaViews", "js", "SKHtmlCanvas.test.mjs");
        var start = new ProcessStartInfo("node", ["--test", script])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var node = Process.Start(start)
            ?? throw new InvalidOperationException("Node.js is required to run the SKHtmlCanvas.js tests.");
        var output = node.StandardOutput.ReadToEndAsync();
        var error = node.StandardError.ReadToEndAsync();
        node.WaitForExit();

        node.ExitCode.Should().Be(0, $"{output.Result}{error.Result}");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlazorBlaze.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("BlazorBlaze.sln not found above the test output.");
    }
}
