using Bunit;
using SkiaSharp.Views.Blazor;

namespace BlazorBlaze.Tests.SkiaViews;

public sealed class CanvasViewInitLifecycleTests : BunitContext
{
    private const string ContentRoot = "./_content/ModelingEvolution.SkiaSharp.Views.Blazor/";

    private readonly BunitJSModuleInterop _canvas;
    private readonly BunitJSModuleInterop _sizeWatcher;

    public CanvasViewInitLifecycleTests()
    {
        _canvas = JSInterop.SetupModule(ContentRoot + "SKHtmlCanvas.js");
        _canvas.Mode = JSRuntimeMode.Loose;
        _sizeWatcher = JSInterop.SetupModule(ContentRoot + "SizeWatcher.js");
        _sizeWatcher.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task SKCanvasView_DisposedWhileInitRasterPending_DoesNotObserveSize()
    {
        var initRaster = _canvas.Setup<bool>("SKHtmlCanvas.initRaster", _ => true);
        Render<SKCanvasView>();

        await DisposeComponentsAsync();
        initRaster.SetResult(true);
        await DrainDispatcherAsync();

        _canvas.VerifyInvoke("SKHtmlCanvas.deinit");
        _sizeWatcher.VerifyNotInvoke("SizeWatcher.observe");
    }

    [Fact]
    public void SKCanvasView_InitRasterFails_ReleasesCanvasAndDoesNotObserveSize()
    {
        _canvas.Setup<bool>("SKHtmlCanvas.initRaster", _ => true).SetResult(false);

        Render<SKCanvasView>();

        _canvas.VerifyInvoke("SKHtmlCanvas.deinit");
        _sizeWatcher.VerifyNotInvoke("SizeWatcher.observe");
    }

    [Fact]
    public async Task SKCanvasView_DisposedWhileObservePending_UnobservesAfterObserveCompletes()
    {
        _canvas.Setup<bool>("SKHtmlCanvas.initRaster", _ => true).SetResult(true);
        var observe = _sizeWatcher.SetupVoid("SizeWatcher.observe", _ => true);
        Render<SKCanvasView>();

        await DisposeComponentsAsync();
        observe.SetVoidResult();
        await DrainDispatcherAsync();

        _sizeWatcher.VerifyInvoke("SizeWatcher.unobserve");
    }

    [Fact]
    public void SKGLView_InitGLReturnsNoContext_ReleasesCanvasAndDoesNotObserveSize()
    {
        Render<SKGLView>();

        _canvas.VerifyInvoke("SKHtmlCanvas.initGL");
        _canvas.VerifyInvoke("SKHtmlCanvas.deinit");
        _sizeWatcher.VerifyNotInvoke("SizeWatcher.observe");
    }

    private Task DrainDispatcherAsync() => Renderer.Dispatcher.InvokeAsync(() => { });
}
