using Bunit;
using Microsoft.JSInterop;
using SkiaSharp.Views.Blazor;
using SkiaSharp.Views.Blazor.Internal;

namespace BlazorBlaze.Tests.SkiaViews;

public sealed class CanvasViewRenderFrameTests : BunitContext
{
    private const string ContentRoot = "./_content/ModelingEvolution.SkiaSharp.Views.Blazor/";

    private readonly BunitJSModuleInterop _canvas;
    private readonly BunitJSModuleInterop _sizeWatcher;

    public CanvasViewRenderFrameTests()
    {
        _canvas = LooseModule("SKHtmlCanvas.js");
        _sizeWatcher = LooseModule("SizeWatcher.js");
        LooseModule("DpiWatcher.js").Setup<double>("DpiWatcher.start", _ => true).SetResult(1);
        _canvas.Setup<bool>("SKHtmlCanvas.initRaster", _ => true).SetResult(true);
    }

    [Fact]
    public async Task SKCanvasView_RenderFrame_PutsThePaintedFrame()
    {
        var renderFrame = RenderSizedCanvas(100, 50);

        await renderFrame();

        var put = _canvas.VerifyInvoke("SKHtmlCanvas.putImageData");
        put.Arguments.Skip(2).Should().Equal(100, 50);
    }

    [Fact]
    public async Task SKCanvasView_PutImageDataFails_FaultsTheRenderFrame()
    {
        _canvas.SetupVoid("SKHtmlCanvas.putImageData", _ => true).SetException(new JSException("putImageData failed"));
        var renderFrame = RenderSizedCanvas(100, 50);

        var frame = async () => await renderFrame();

        await frame.Should().ThrowAsync<JSException>().WithMessage("putImageData failed");
    }

    private Func<Task> RenderSizedCanvas(float width, float height)
    {
        Render<SKCanvasView>();
        var size = (DotNetObjectReference<FloatFloatActionHelper>)_sizeWatcher.VerifyInvoke("SizeWatcher.observe").Arguments[2]!;
        size.Value.Invoke(width, height);
        var frame = (DotNetObjectReference<ActionHelper>)_canvas.VerifyInvoke("SKHtmlCanvas.initRaster").Arguments[2]!;
        return frame.Value.Invoke;
    }

    private BunitJSModuleInterop LooseModule(string fileName)
    {
        var module = JSInterop.SetupModule(ContentRoot + fileName);
        module.Mode = JSRuntimeMode.Loose;
        return module;
    }
}
