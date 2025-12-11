# BlazorBlaze

High-performance rendering library for Blazor WebAssembly with three rendering technologies.

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
        // Circle
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

**Performance (stress test with 20K polygons @ 60 FPS):**
- Render time: ~2.5ms per frame
- Transfer rate: ~1.1 MB/s
- Smooth, consistent frame delivery
- Supports transformation matrices (Rotation, Scale, Skew, Offset)

**Server (using BlazorBlaze.Server):**
```csharp
using BlazorBlaze.Server;
using BlazorBlaze.VectorGraphics;

app.UseWebSockets();

// Minimal API-style endpoint with IRemoteCanvas
app.MapVectorGraphicsEndpoint("/ws/stream", async (IRemoteCanvas canvas, CancellationToken ct) =>
{
    var points = new SKPoint[] { /* polygon vertices */ };

    while (!ct.IsCancellationRequested)
    {
        canvas.Begin();

        // Draw with transformation support
        canvas.DrawPolygon(points, new DrawContext
        {
            Stroke = new RgbColor(255, 100, 100),
            Thickness = 2,
            Offset = new SKPoint(100, 100),    // Translate
            Rotation = 45f,                     // Degrees
            Scale = new SKPoint(1.5f, 1.5f)    // Scale
        });

        await canvas.FlushAsync(ct);
        await Task.Delay(16, ct); // ~60 FPS
    }
});
```

**Client:**
```csharp
@using BlazorBlaze.VectorGraphics

private VectorGraphicsDecoder _decoder = new();
private ClientWebSocket _ws = new();

// Connect and receive
await _ws.ConnectAsync(new Uri("ws://localhost:5100/ws/stream"), CancellationToken.None);
var result = await _ws.ReceiveAsync(buffer, token);
_decoder.Decode(buffer.Slice(0, result.Count));

// Render
_decoder.Render(canvas);
```

## Running the Sample App

### Quick Start (Recommended)

```bash
cd samples/SampleApp
./run.sh
```

This builds in Release mode with AOT compilation and starts the server in the background.

**IMPORTANT: Do NOT use `dotnet publish` or `dotnet run` directly!** Always use `./run.sh` - it handles cleaning, building, and starting the server correctly.

### Manual Clean (if needed)

If you encounter integrity hash errors or need a fresh build:

```bash
cd samples/SampleApp

# Kill any existing server
fuser -k 5100/tcp 2>/dev/null || true

# Clean everything
dotnet clean
rm -rf bin obj publish
rm -rf ../SampleApp.Client/bin ../SampleApp.Client/obj

# Then run the script
./run.sh
```

AOT (Ahead-of-Time) compilation significantly improves runtime performance for compute-intensive operations like the stress test (20K polygons @ 30 FPS). The `run.sh` script handles this automatically.

Opens at http://localhost:5100

Demo pages:
- `/canvas` - BlazorCanvas with draggable shapes
- `/barchart` - Bar chart demo
- `/timeseries` - Time series chart with live updates
- `/stress` - WebSocket streaming 20K polygons @ 30 FPS

## Testing with MCP Playwright

```
# Navigate to app
mcp__playwright__browser_navigate url="http://localhost:5100"

# Get page structure
mcp__playwright__browser_snapshot

# Click navigation
mcp__playwright__browser_click element="Canvas Demo" ref="[ref]"

# Take screenshot
mcp__playwright__browser_take_screenshot

# Check for errors
mcp__playwright__browser_console_messages level="error"
```

## Project Structure

```
src/
  BlazorBlaze/                        # Main library (WASM-compatible)
    Charts/                           # BarChart, TimeSeriesChart
    VectorGraphics/                   # VectorGraphicsEncoder, Decoder, DrawContext
  BlazorBlaze.Server/                 # Server extensions (ASP.NET Core)
    IRemoteCanvas                     # Server-side canvas interface
    WebSocketRemoteCanvas             # WebSocket implementation
    VectorGraphicsEndpointExtensions  # MapVectorGraphicsEndpoint
samples/
  SampleApp/                          # Server + WebSocket endpoints
  SampleApp.Client/                   # Blazor WASM client
```

## Dependencies

- .NET 10.0
- SkiaSharp
- SkiaSharp.Views.Blazor
- ModelingEvolution.Drawing
