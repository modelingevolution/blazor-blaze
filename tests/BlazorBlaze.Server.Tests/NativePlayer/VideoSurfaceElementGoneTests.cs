using BlazorBlaze.Server.NativePlayer;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ModelingEvolution.EventAggregator;
using NSubstitute;
using EventAggregator = ModelingEvolution.EventAggregator.EventAggregator;

namespace BlazorBlaze.Server.Tests.NativePlayer;

/// <summary>
/// Bug 075: at STATION STOP the run screen re-rendered and the kiosk placeholder element was gone when
/// <c>VideoSurface.OnAfterRenderAsync</c> called <c>createAdapter</c>. The browser threw
/// "Cannot read properties of null (reading 'getBoundingClientRect')", the exception escaped
/// <c>OnAfterRenderAsync</c>, and Blazor Server killed the circuit (red bar). A video surface must survive
/// a failed or element-less adapter creation and try again on ITS OWN next render. A parent re-render with unchanged
/// parameters does not re-render the surface, so it is no retry (pinned below); a removed surface is disposed and the
/// replacing instance creates its adapter on its first render.
/// </summary>
public sealed class VideoSurfaceElementGoneTests : BunitContext
{
    // The exact message Chromium produced at Pamet (docs/bugs/bug-075/rw2-circuit-crash-excerpt.log).
    private const string BrowserNullElementError =
        "Cannot read properties of null (reading 'getBoundingClientRect')";

    private readonly List<PlayerInitialized> _initializedEvents = [];
    private readonly List<string> _published = [];
    private readonly BunitJSModuleInterop _moduleInterop;
    private readonly CreateAdapterHandler _createAdapter = new();
    private readonly CapturingLogger _log = new();
    private readonly EventAggregator _ea = new(new NullForwarder(), new EventAggregatorPool());

    public VideoSurfaceElementGoneTests()
    {
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        Services.AddSingleton(kioskDetector);
        Services.AddSingleton<IEventAggregator>(_ea);
        Services.AddSingleton<ILogger<VideoSurface>>(_log);

        _moduleInterop = JSInterop.SetupModule(VideoSurfaceConstants.ModulePath);
        _moduleInterop.Mode = JSRuntimeMode.Loose;
        _moduleInterop.AddInvocationHandler(_createAdapter);

        _ea.GetEvent<PlayerInitialized>().Subscribe(e => { _initializedEvents.Add(e); _published.Add("init"); });
        _ea.GetEvent<PlayRequested>().Subscribe(_ => _published.Add("play"));
        _ea.GetEvent<PlayerDestroyed>().Subscribe(_ => _published.Add("destroyed"));
    }

    [Fact]
    public void OnAfterRenderAsync_CreateAdapterThrowsBecauseElementIsGone_ComponentSurvivesAndRetriesOnItsOwnNextRender()
    {
        _createAdapter.Throw(new JSException(BrowserNullElementError));

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "run-screen"));

        // The failure must not escape OnAfterRenderAsync: in Blazor Server that is what kills the circuit.
        Renderer.UnhandledException.IsCompleted.Should().BeFalse(
            "a JS interop failure in a video surface must never kill the page");
        _initializedEvents.Should().BeEmpty();

        // The element is back: the browser now creates the adapter. cut.Render() is the surface's OWN re-render;
        // see ParentRerender_* for what a parent render does.
        _createAdapter.Return(Substitute.For<IJSObjectReference>());

        cut.Render();

        _moduleInterop.Invocations.Count(i => i.Identifier == "createAdapter").Should().Be(2);
        _initializedEvents.Should().ContainSingle(e => e.Id == "run-screen");
        Renderer.UnhandledException.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void OnAfterRenderAsync_CreateAdapterReturnsNoAdapterForMissingElement_ComponentSurvivesAndRetriesOnItsOwnNextRender()
    {
        // The fixed video-surface.js returns null (no adapter) for a null or detached element.
        _createAdapter.Return(null);

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "run-screen"));

        Renderer.UnhandledException.IsCompleted.Should().BeFalse(
            "no adapter is a normal state while the element is missing, not a crash");
        _initializedEvents.Should().BeEmpty();

        _createAdapter.Return(Substitute.For<IJSObjectReference>());

        cut.Render();

        _moduleInterop.Invocations.Count(i => i.Identifier == "createAdapter").Should().Be(2);
        _initializedEvents.Should().ContainSingle(e => e.Id == "run-screen");
    }

    [Fact]
    public void OnAfterRenderAsync_AdapterCreated_LaterRendersDoNotCreateAnotherAdapter()
    {
        _createAdapter.Return(Substitute.For<IJSObjectReference>());

        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        cut.Render();
        cut.Render();

        _moduleInterop.Invocations.Count(i => i.Identifier == "createAdapter").Should().Be(1);
        _initializedEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task DisposeAsync_AdapterNeverCreated_DoesNotThrow()
    {
        _createAdapter.Throw(new JSException(BrowserNullElementError));

        Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));

        var act = async () => await DisposeComponentsAsync();

        await act.Should().NotThrowAsync();
        _moduleInterop.Invocations.Should().NotContain(i => i.Identifier == "dispose");
    }

    [Fact]
    public void OnAfterRenderAsync_CreateAdapterKeepsFailing_LogsOneWarning()
    {
        _createAdapter.Throw(new JSException(BrowserNullElementError));

        var cut = Render<VideoSurface>(p =>
            p.Add(vs => vs.StreamUrl, "http://localhost/stream"));
        cut.Render();
        cut.Render();

        _moduleInterop.Invocations.Count(i => i.Identifier == "createAdapter").Should().Be(3);
        _log.Levels.Count(l => l == LogLevel.Warning).Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_WhileGetRectIsPending_ReleasesAdapterAndPublishesNothingAfterDestroyed()
    {
        // PR #26 review F1 (reviewer probe adopted): STOP removes the surface during the getRect round trip.
        var rect = new TaskCompletionSource<PlayerRect>();
        var adapter = Substitute.For<IJSObjectReference>();
        adapter.InvokeAsync<PlayerRect>("getRect", Arg.Any<object?[]?>()).Returns(new ValueTask<PlayerRect>(rect.Task));
        _createAdapter.Return(adapter);

        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "run-screen"));
        await DisposeComponentsAsync();
        await Renderer.Dispatcher.InvokeAsync(() => rect.SetResult(new PlayerRect()));
        await WaitUntil(() => Disposed(adapter));

        adapter.ReceivedCalls().Should().Contain(c => c.GetArguments().FirstOrDefault() as string == "dispose",
            "the JS adapter keeps html.np-video set (page see-through, bug 590) until it is disposed");
        adapter.ReceivedCalls().Should().Contain(c => c.GetMethodInfo().Name == "DisposeAsync",
            "the adapter reference must be released");
        _published.Should().Equal(["destroyed"], "nothing may be published for a player after PlayerDestroyed");
    }

    [Fact]
    public async Task DisposeAsync_WhileCreateAdapterIsPending_ReleasesAdapterAndPublishesNothingAfterDestroyed()
    {
        // The case the first round handled; kept as a control next to the getRect and import cases.
        var adapter = Substitute.For<IJSObjectReference>();

        Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "run-screen"));
        await DisposeComponentsAsync();
        await Renderer.Dispatcher.InvokeAsync(() => _createAdapter.Return(adapter));
        await WaitUntil(() => Disposed(adapter));

        adapter.ReceivedCalls().Should().Contain(c => c.GetArguments().FirstOrDefault() as string == "dispose");
        _published.Should().Equal(["destroyed"]);
    }

    [Fact]
    public async Task DisposeAsync_WhileModuleImportIsPending_ReleasesModuleAndCreatesNoAdapter()
    {
        // PR #26 review F3: the import is still in flight when STOP removes the surface. A separate context,
        // because the import must be answered by a handler the test controls, not by SetupModule.
        await using var ctx = new BunitContext();
        var kioskDetector = Substitute.For<IKioskDetector>();
        kioskDetector.IsKiosk.Returns(true);
        ctx.Services.AddSingleton(kioskDetector);
        ctx.Services.AddSingleton<IEventAggregator>(new EventAggregator(new NullForwarder(), new EventAggregatorPool()));
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var import = new ImportHandler();
        ctx.JSInterop.AddInvocationHandler(import);
        var module = Substitute.For<IJSObjectReference>();

        ctx.Render<VideoSurface>(p => p.Add(vs => vs.StreamUrl, "http://localhost/stream"));
        import.Invocations.Should().ContainSingle("the import must be pending when the surface is disposed");
        await ctx.DisposeComponentsAsync();
        await ctx.Renderer.Dispatcher.InvokeAsync(() => import.Return(module));
        await WaitUntil(() => module.ReceivedCalls().Any());

        module.ReceivedCalls().Should().Contain(c => c.GetMethodInfo().Name == "DisposeAsync",
            "the module reference must be released");
        module.ReceivedCalls().Should().NotContain(c => c.GetArguments().FirstOrDefault() as string == "createAdapter");
    }

    [Fact]
    public async Task ParentRerender_SameParametersAfterFailedCreate_DoesNotRetry()
    {
        // PR #26 review F2/F4: pins what is true. Blazor skips a child whose primitive parameters did not change,
        // so a parent re-render is not a retry. (In rw2 a STOP that removes the surface disposes it, and the
        // instance that replaces it creates its adapter on its first render.)
        _createAdapter.Throw(new JSException(BrowserNullElementError));
        var parent = Render<Parent>();
        _createAdapter.Return(Substitute.For<IJSObjectReference>());

        await parent.Instance.RerenderAsync();
        await parent.Instance.RerenderAsync();

        _moduleInterop.Invocations.Count(i => i.Identifier == "createAdapter").Should().Be(1);
        _initializedEvents.Should().BeEmpty("the slot stays blank until the surface itself renders again");
    }

    [Fact]
    public async Task ParentRerender_ChangedParameterAfterFailedCreate_RetriesCreateAdapter()
    {
        _createAdapter.Throw(new JSException(BrowserNullElementError));
        var parent = Render<Parent>();
        _createAdapter.Return(Substitute.For<IJSObjectReference>());

        await parent.Instance.RerenderAsync(frameWidth: 1280);

        _moduleInterop.Invocations.Count(i => i.Identifier == "createAdapter").Should().Be(2);
        _initializedEvents.Should().ContainSingle(e => e.Id == "run-screen");
    }

    [Fact]
    public void CreateAdapterNullResult_BlazorWireFormat_DeserializesToNoAdapter()
    {
        // The fixed createAdapter returns null for a missing element. blazor.server.js sends a null object
        // reference as {"__jsObjectId":-1}; the .NET side must read that as "no adapter", not as a live reference,
        // or VideoSurface would call getRect on an object that does not exist.
        var runtime = new WireFormatJSRuntime();

        var adapter = runtime.Deserialize<IJSObjectReference?>("{\"__jsObjectId\":-1}");

        adapter.Should().BeNull();
        runtime.Deserialize<IJSObjectReference?>("{\"__jsObjectId\":5}").Should().NotBeNull(
            "the same converter reads a live reference, so the null above is the sentinel, not a converter that never answers");
    }

    private sealed class WireFormatJSRuntime : JSRuntime
    {
        public T? Deserialize<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);

        protected override void BeginInvokeJS(long taskId, string identifier, string? argsJson,
            JSCallResultType resultType, long targetInstanceId) => throw new NotSupportedException();

        protected override void EndInvokeDotNet(Microsoft.JSInterop.Infrastructure.DotNetInvocationInfo invocationInfo,
            in Microsoft.JSInterop.Infrastructure.DotNetInvocationResult invocationResult) => throw new NotSupportedException();
    }

    private static bool Disposed(IJSObjectReference reference) =>
        reference.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "DisposeAsync");

    // The continuation after a completed JS call runs after the dispatcher call returns. Wait for the call the
    // assertion needs (it is also the liveness anchor of the "nothing published" assertions); the deadline turns
    // "never happened" into a failing assertion instead of a hang.
    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    /// <summary>A parent like rw2's CameraPreview: primitive parameters only.</summary>
    private sealed class Parent : ComponentBase
    {
        private int _frameWidth = 1920;
        private int _renders;

        public Task RerenderAsync(int? frameWidth = null) => InvokeAsync(() =>
        {
            _frameWidth = frameWidth ?? _frameWidth;
            _renders++;
            StateHasChanged();
        });

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, _renders);
            builder.OpenComponent<VideoSurface>(1);
            builder.AddAttribute(2, nameof(VideoSurface.StreamUrl), "http://localhost/stream");
            builder.AddAttribute(3, nameof(VideoSurface.PlayerId), "run-screen");
            builder.AddAttribute(4, nameof(VideoSurface.FrameWidth), _frameWidth);
            builder.CloseComponent();
        }
    }

    private sealed class ImportHandler()
        : JSRuntimeInvocationHandlerBase<IJSObjectReference>(i => i.Identifier == "import", isCatchAllHandler: false)
    {
        public void Return(IJSObjectReference module) => SetResultBase(module);
    }

    private sealed class CapturingLogger : ILogger<VideoSurface>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    /// <summary>
    /// bUnit refuses <c>Setup&lt;IJSObjectReference&gt;</c>, so <c>createAdapter</c> is answered by this handler: it
    /// throws like the browser, returns no adapter, or returns an adapter, whichever was set last.
    /// </summary>
    private sealed class CreateAdapterHandler()
        : JSRuntimeInvocationHandlerBase<IJSObjectReference>(i => i.Identifier == "createAdapter", isCatchAllHandler: false)
    {
        public void Throw(Exception exception) => SetExceptionBase(exception);
        public void Return(IJSObjectReference? adapter) => SetResultBase(adapter!);
    }
}
