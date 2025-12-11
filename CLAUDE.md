# BlazorBlaze - Claude Code Context

## Quick Start

```bash
cd samples/SampleApp
./run.sh
```

Opens at `http://localhost:5100`

## Three Rendering Technologies

### 1. BlazorCanvas + BlazeEngine (Scene Graph)
- Namespace: `BlazorBlaze`
- Components: `BlazorCanvas`, `BlazeEngine`
- Controls: `CircleControl`, `RectangleControl`, `PolygonControl`
- Extension: `control.EnableDrag()` for drag-and-drop
- Events: `OnClick`, `OnDrag`
- Demo: `/canvas`

### 2. Charts
- Namespace: `BlazorBlaze.Charts`
- Classes: `BarChart`, `TimeSeriesChart`, `TimeSeriesF`
- Use `SKCanvasView` from SkiaSharp.Views.Blazor
- Set `Location` and `Size` properties, then call `Render(canvas)`
- Demos: `/barchart`, `/timeseries`

### 3. VectorGraphics (WebSocket Streaming)
- Namespace: `BlazorBlaze.VectorGraphics`
- Server: `RenderingStream` wraps WebSocket
- Client: `VectorGraphicsDecoder` decodes and renders
- Binary protocol for high-performance streaming
- Demo: `/stress` (20K polygons @ 30 FPS)

## Testing with MCP Playwright

```bash
# Start server first
cd samples/SampleApp && ./run.sh
```

Then use MCP Playwright tools:
- `mcp__playwright__browser_navigate` - Go to http://localhost:5100
- `mcp__playwright__browser_snapshot` - Get accessibility tree
- `mcp__playwright__browser_click` - Click navigation links
- `mcp__playwright__browser_take_screenshot` - Capture page
- `mcp__playwright__browser_console_messages` - Check for errors

## Key Patterns

### BlazorCanvas Setup
```csharp
private BlazeEngine _engine = new BlazeEngine(Sizes.HD);
<BlazorCanvas Engine="_engine" Size="Sizes.HD" />
_engine.Scene.AddControl(control);
```

### Chart Setup
```csharp
_chart.Location = new SKPoint(0, 0);
_chart.Size = new SKSize(e.Info.Width, e.Info.Height);
_chart.Render(canvas);
```

### WebSocket Streaming
```csharp
// Server
var stream = new RenderingStream(websocket);
stream.DrawPolygon(vertices, color);
await stream.FlushAsync();

// Client
_decoder.Decode(buffer);
_decoder.Render(canvas);
```

## Dependencies

- SkiaSharp
- SkiaSharp.Views.Blazor
- ModelingEvolution.Drawing (for `Point<float>`, `Polygon<float>`)
- .NET 10.0

## Project Structure

```
src/
├── BlazorBlaze/                         # Main library
│   ├── Charts/                          # BarChart, TimeSeriesChart
│   └── VectorGraphics/                  # RenderingStream, Decoder
samples/
├── SampleApp/                           # Server (WebSocket endpoint)
└── SampleApp.Client/                    # Blazor WASM client
tests/
├── BlazorBlaze.Tests/                   # Unit tests
└── BlazorBlaze.E2ETests/                # End-to-end tests
benchmarks/
└── BlazorBlaze.Benchmarks/              # Performance benchmarks
```
