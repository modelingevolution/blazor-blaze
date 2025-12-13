# BlazorBlaze

High-performance rendering library for Blazor WebAssembly with three rendering technologies.

[![NuGet](https://img.shields.io/nuget/v/BlazorBlaze.svg)](https://www.nuget.org/packages/BlazorBlaze)

## Installation

```xml
<!-- Client-side (Blazor WASM) -->
<PackageReference Include="BlazorBlaze" />

<!-- Server-side (ASP.NET Core WebSocket streaming) -->
<PackageReference Include="BlazorBlaze.Server" />
```

## Rendering Technologies

### 1. BlazorCanvas + BlazeEngine (Scene Graph)

Interactive scene graph with draggable controls for building visual editors.

```csharp
@using BlazorBlaze
@using ModelingEvolution.Drawing
@using SkiaSharp

<BlazorCanvas Engine="_engine" Size="Sizes.HD" />

@code {
    private BlazeEngine _engine = new BlazeEngine(Sizes.HD);

    protected override void OnInitialized()
    {
        // Circle with drag support
        var circle = new CircleControl(new SKPoint(100, 100), 50);
        circle.Fill = SKColors.Red;
        circle.EnableDrag();
        circle.OnClick += (s, e) => { /* handle click */ };
        _engine.Scene.AddControl(circle);

        // Rectangle
        var rect = new RectangleControl(new SKRect(200, 50, 350, 150));
        rect.Fill = SKColors.Blue;
        rect.EnableDrag();
        _engine.Scene.AddControl(rect);

        // Polygon
        var points = new Point<float>[] { new(300, 200), new(350, 300), new(250, 300) };
        var polygon = new PolygonControl(new Polygon<float>(points));
        polygon.Fill = SKColors.Green;
        polygon.EnableDrag();
        _engine.Scene.AddControl(polygon);
    }
}
```

### 2. Charts (BarChart, TimeSeriesChart)

SkiaSharp-based charts for data visualization.

```csharp
@using BlazorBlaze.Charts
@using SkiaSharp
@using SkiaSharp.Views.Blazor

<SKCanvasView OnPaintSurface="OnPaintSurface" EnableRenderLoop="true" />

@code {
    private BarChart _chart = new();
    private string[] _labels = { "A", "B", "C" };
    private float[] _values = { 45.2f, 72.8f, 33.1f };

    protected override void OnInitialized()
    {
        _chart.SetData(
            title: "Demo",
            labels: _labels, labelCount: _labels.Length,
            values: _values, valueCount: _values.Length,
            minScale: 0f, maxScale: 100f,
            units: "%", valueFormat: "{0:F1}",
            enableMinMaxTracking: true
        );
    }

    private void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        _chart.Location = new SKPoint(0, 0);
        _chart.Size = new SKSize(e.Info.Width, e.Info.Height);
        _chart.Render(e.Surface.Canvas);
    }
}
```

### 3. VectorGraphics (WebSocket Streaming)

High-performance binary streaming for real-time graphics over WebSocket.

**Protocol v2 Features:**
- Multi-layer rendering with z-ordering (16 layers)
- Stateful context (stroke, fill, transforms persist across draw calls)
- Lock-free rendering with layer pooling
- Delta-encoded points with varint+zigzag compression
- Save/Restore for hierarchical transforms

**Performance (stress test with 20K points @ 60 FPS):**
- Render time: ~2.5ms per frame
- Transfer rate: ~1.1 MB/s
- Zero-allocation rendering path

#### Server (Protocol v2 - Recommended)

```csharp
using BlazorBlaze.Server;
using BlazorBlaze.VectorGraphics;

app.UseWebSockets();

app.MapVectorGraphicsEndpointV2("/ws/stream", async (IRemoteCanvasV2 canvas, CancellationToken ct) =>
{
    var points = new SKPoint[] { /* polygon vertices */ };

    while (!ct.IsCancellationRequested)
    {
        canvas.BeginFrame();

        // Layer 0 - Background elements (stateful context)
        var bg = canvas.Layer(0);
        bg.SetStroke(new RgbColor(100, 100, 100));
        bg.SetThickness(1);
        bg.DrawRectangle(0, 0, 800, 600);

        // Layer 1 - Animated content with transforms
        var layer = canvas.Layer(1);
        layer.SetStroke(new RgbColor(255, 100, 100));
        layer.SetThickness(2);
        layer.Save();
        layer.Translate(400, 300);
        layer.Rotate(angle);
        layer.DrawPolygon(points);
        layer.Restore();

        // Layer 2 - Static overlay (Remain = no update)
        canvas.Layer(2).Remain();

        await canvas.FlushAsync(ct);
        await Task.Delay(16, ct); // ~60 FPS
    }
});
```

#### Client (Protocol v2)

```csharp
@using BlazorBlaze.VectorGraphics
@inject ILoggerFactory LoggerFactory

<SKCanvasView @ref="_canvasView" OnPaintSurface="OnPaintSurface" EnableRenderLoop="true" />

@code {
    private RenderingStreamV2? _stream;
    private SKCanvasView? _canvasView;

    protected override void OnInitialized()
    {
        _stream = new RenderingStreamV2(1200, 800, LoggerFactory);
    }

    private async Task Connect()
    {
        var uri = new Uri("ws://localhost:5100/ws/stream");
        await _stream!.ConnectAsync(uri);
    }

    private void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Black);
        _stream?.Render(canvas);  // Lock-free, composites all layers
    }

    // Pool statistics available for monitoring
    // _stream.PoolInUse, _stream.PoolCached, _stream.PoolTotalCreated
}
```

#### Server (Protocol v1 - Simple)

```csharp
app.MapVectorGraphicsEndpoint("/ws/simple", async (IRemoteCanvas canvas, CancellationToken ct) =>
{
    canvas.Begin();
    canvas.DrawPolygon(points, new DrawContext
    {
        Stroke = new RgbColor(255, 100, 100),
        Thickness = 2,
        Offset = new SKPoint(100, 100),
        Rotation = 45f,
        Scale = new SKPoint(1.5f, 1.5f)
    });
    await canvas.FlushAsync(ct);
});
```

## Running the Sample App

### Quick Start

```bash
cd samples/SampleApp
./run.sh
```

Opens at http://localhost:5100

Demo pages:
- `/canvas` - BlazorCanvas with draggable shapes
- `/barchart` - Bar chart demo
- `/timeseries` - Time series chart with live updates
- `/stress` - WebSocket streaming 20K points @ 60 FPS (Protocol v2)

### Manual Build

```bash
# Clean build
cd samples/SampleApp
dotnet clean
rm -rf bin obj publish
rm -rf ../SampleApp.Client/bin ../SampleApp.Client/obj
./run.sh
```

## Architecture

### VectorGraphics Protocol v2 Internals

```
Server Side:
┌─────────────────┐    ┌──────────────────┐    ┌─────────────┐
│ IRemoteCanvasV2 │───>│ VectorEncoderV2  │───>│  WebSocket  │
│   ILayerCanvas  │    │ (binary protocol)│    │   (send)    │
└─────────────────┘    └──────────────────┘    └─────────────┘

Client Side:
┌─────────────┐    ┌───────────────────┐    ┌────────────────┐    ┌───────────┐
│  WebSocket  │───>│ VectorStreamDecoder│───>│ RenderingStage │───>│ LayerPool │
│  (receive)  │    │ (parse + decode)   │    │ (frame mgmt)   │    │ (recycle) │
└─────────────┘    └───────────────────┘    └────────────────┘    └───────────┘
                                                     │
                                                     v
                                            ┌────────────────┐
                                            │ RenderingStreamV2│
                                            │ (lock-free render)│
                                            └────────────────┘
```

**Key Components:**

- `LayerPool` - Thread-safe pool of `LayerCanvas` instances using `ConcurrentBag`
- `RenderingStage` - Manages frame lifecycle with lock-free consumer access
- `Ref<T>` / `RefArray<T>` - Reference-counted smart pointers for safe frame sharing
- `Lease<T>` - Pool return wrapper, returns layer to pool on dispose

## Project Structure

```
src/
  BlazorBlaze/                        # Main library (WASM-compatible)
    Charts/                           # BarChart, TimeSeriesChart
    VectorGraphics/                   # Streaming, Decoder, RenderingStream
      Protocol/                       # IStage, LayerPool, LayerCanvas
    ValueTypes/                       # Ref<T>, RefArray<T>, Lease<T>
  BlazorBlaze.Server/                 # Server extensions (ASP.NET Core)
    IRemoteCanvasV2                   # Multi-layer canvas interface
    VectorEncoderV2                   # Binary protocol encoder
samples/
  SampleApp/                          # Server + WebSocket endpoints
  SampleApp.Client/                   # Blazor WASM client
tests/
  BlazorBlaze.Tests/                  # Unit tests (177 tests)
benchmarks/
  BlazorBlaze.Benchmarks/             # Performance benchmarks
```

## Dependencies

- .NET 10.0
- SkiaSharp
- SkiaSharp.Views.Blazor
- ModelingEvolution.Drawing

## License

MIT
