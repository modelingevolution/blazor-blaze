using BlazorBlaze.Server.NativePlayer;
using Bunit;
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
/// a failed or element-less adapter creation and try again on the next render.
/// </summary>
public sealed class VideoSurfaceElementGoneTests : BunitContext
{
    // The exact message Chromium produced at Pamet (docs/bugs/bug-075/rw2-circuit-crash-excerpt.log).
    private const string BrowserNullElementError =
        "Cannot read properties of null (reading 'getBoundingClientRect')";

    private readonly List<PlayerInitialized> _initializedEvents = [];
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

        _ea.GetEvent<PlayerInitialized>().Subscribe(e => _initializedEvents.Add(e));
    }

    [Fact]
    public void OnAfterRenderAsync_CreateAdapterThrowsBecauseElementIsGone_ComponentSurvivesAndRetriesOnNextRender()
    {
        _createAdapter.Throw(new JSException(BrowserNullElementError));

        var cut = Render<VideoSurface>(p => p
            .Add(vs => vs.StreamUrl, "http://localhost/stream")
            .Add(vs => vs.PlayerId, "run-screen"));

        // The failure must not escape OnAfterRenderAsync: in Blazor Server that is what kills the circuit.
        Renderer.UnhandledException.IsCompleted.Should().BeFalse(
            "a JS interop failure in a video surface must never kill the page");
        _initializedEvents.Should().BeEmpty();

        // The element is back: the browser now creates the adapter.
        _createAdapter.Return(Substitute.For<IJSObjectReference>());

        cut.Render();

        _moduleInterop.Invocations.Count(i => i.Identifier == "createAdapter").Should().Be(2);
        _initializedEvents.Should().ContainSingle(e => e.Id == "run-screen");
        Renderer.UnhandledException.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void OnAfterRenderAsync_CreateAdapterReturnsNoAdapterForMissingElement_ComponentSurvivesAndRetriesOnNextRender()
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
