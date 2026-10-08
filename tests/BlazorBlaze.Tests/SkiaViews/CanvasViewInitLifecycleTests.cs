using Bunit;
using SkiaSharp.Views.Blazor;

namespace BlazorBlaze.Tests.SkiaViews;

public sealed class CanvasViewInitLifecycleTests : BunitContext
{
    private readonly SkiaViewModules _modules;
    private readonly BunitJSModuleInterop _canvas;
    private readonly BunitJSModuleInterop _sizeWatcher;
    private readonly BunitJSModuleInterop _dpiWatcher;

    public CanvasViewInitLifecycleTests()
    {
        _modules = new SkiaViewModules(JSInterop);
        _canvas = _modules.Canvas;
        _sizeWatcher = _modules.SizeWatcher;
        _dpiWatcher = _modules.DpiWatcher;
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
        _modules.SetInitRasterResult(false);

        Render<SKCanvasView>();

        _sizeWatcher.VerifyNotInvoke("SizeWatcher.observe");
        _canvas.VerifyInvoke("SKHtmlCanvas.deinit");
    }

    [Fact]
    public async Task SKCanvasView_DisposedWhileObservePending_UnobservesAfterObserveCompletes()
    {
        _modules.SetInitRasterResult(true);
        var observe = _sizeWatcher.SetupVoid("SizeWatcher.observe", _ => true);
        Render<SKCanvasView>();

        await DisposeComponentsAsync();
        observe.SetVoidResult();
        await DrainDispatcherAsync();

        _sizeWatcher.VerifyInvoke("SizeWatcher.unobserve");
    }

    [Fact]
    public async Task SKCanvasView_DisposedWhileDpiWatcherImportPending_StopsDpiWatcher()
    {
        _modules.SetInitRasterResult(true);
        var start = _dpiWatcher.Setup<double>("DpiWatcher.start", _ => true);
        Render<SKCanvasView>();
        _dpiWatcher.VerifyInvoke("DpiWatcher.start");

        await DisposeComponentsAsync();
        start.SetResult(1);
        await DrainDispatcherAsync();

        _dpiWatcher.VerifyInvoke("DpiWatcher.stop");
    }

    [Fact]
    public async Task SKCanvasView_RenderedInTwoJSRuntimes_StartsDpiWatcherInEach()
    {
        await using var other = new BunitContext();
        var otherModules = new SkiaViewModules(other.JSInterop);
        _modules.SetInitRasterResult(true);
        otherModules.SetInitRasterResult(true);

        Render<SKCanvasView>();
        other.Render<SKCanvasView>();

        _dpiWatcher.VerifyInvoke("DpiWatcher.start");
        otherModules.DpiWatcher.VerifyInvoke("DpiWatcher.start");
    }

    [Fact]
    public void SKGLView_InitGLReturnsNoContext_ReleasesCanvasAndDoesNotObserveSize()
    {
        Render<SKGLView>();

        _canvas.VerifyInvoke("SKHtmlCanvas.initGL");
        _sizeWatcher.VerifyNotInvoke("SizeWatcher.observe");
        _canvas.VerifyInvoke("SKHtmlCanvas.deinit");
    }

    private Task DrainDispatcherAsync() => Renderer.Dispatcher.InvokeAsync(() => { });

    private sealed class SkiaViewModules
    {
        private const string ContentRoot = "./_content/ModelingEvolution.SkiaSharp.Views.Blazor/";

        public SkiaViewModules(BunitJSInterop jsInterop)
        {
            Canvas = SetupLooseModule(jsInterop, "SKHtmlCanvas.js");
            SizeWatcher = SetupLooseModule(jsInterop, "SizeWatcher.js");
            DpiWatcher = SetupLooseModule(jsInterop, "DpiWatcher.js");
        }

        public BunitJSModuleInterop Canvas { get; }
        public BunitJSModuleInterop SizeWatcher { get; }
        public BunitJSModuleInterop DpiWatcher { get; }

        public void SetInitRasterResult(bool result) =>
            Canvas.Setup<bool>("SKHtmlCanvas.initRaster", _ => true).SetResult(result);

        private static BunitJSModuleInterop SetupLooseModule(BunitJSInterop jsInterop, string fileName)
        {
            var module = jsInterop.SetupModule(ContentRoot + fileName);
            module.Mode = JSRuntimeMode.Loose;
            return module;
        }
    }
}
