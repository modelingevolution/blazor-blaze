namespace BlazorBlaze.Server.Tests.NativePlayer;

/// <summary>
/// Bug 590: the kiosk page is see-through only while a native video view is on it. The rule lives in two static
/// files that no component test evaluates, so their text is pinned here. (The CSS cascade itself is evaluated by the
/// host application's browser tests.)
/// </summary>
public sealed class NativePlayerTransparencyAssetsTests
{
    private static string Asset(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "src", "BlazorBlaze.Server", "wwwroot", name);
            if (File.Exists(path)) return File.ReadAllText(path);
            dir = dir.Parent;
        }
        throw new FileNotFoundException(name);
    }

    private static string WithoutComments(string css) =>
        System.Text.RegularExpressions.Regex.Replace(css, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);

    [Fact]
    public void TheBodyIsTransparentOnlyUnderTheVideoClass()
    {
        var css = WithoutComments(Asset("native-player.css"));

        css.Should().Contain("html.kiosk.np-video body");
        css.Should().NotContain("html.kiosk body", "a bare kiosk rule makes every page see-through");
    }

    [Fact]
    public void NoHasSelector_BecauseTheKioskWebKitGtkDropsIt()
    {
        WithoutComments(Asset("native-player.css")).Should().NotContain(":has(");
    }

    [Fact]
    public void TheVideoClassFollowsTheLiveAdapters()
    {
        var js = Asset("video-surface.js");

        js.Should().Contain("classList.toggle(VIDEO_CLASS, _activeAdapters.size > 0)");
        js.Should().Contain("VIDEO_CLASS = \"np-video\"");
    }
}
