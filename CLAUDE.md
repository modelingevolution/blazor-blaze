# BlazorBlaze - Claude Code Context

## CRITICAL RULES

**Do NOT change the design if tests fail. Always ask for input from the user!**

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
- Client namespace: `BlazorBlaze.VectorGraphics`
- Server namespace: `BlazorBlaze.Server`
- Server: `IRemoteCanvas` interface with `MapVectorGraphicsEndpoint` extension
- Client: `VectorGraphicsDecoder` decodes and renders
- Binary protocol with transformation matrix support (Rotation, Scale, Skew, Offset)
- Demo: `/stress` (20K polygons @ 60 FPS)

## VectorGraphics Protocol V2 Design Rules

**CRITICAL: The protocol and callback interface MUST mimic SKCanvas methods.**

The `IDecoderCallbackV2` interface mirrors SKCanvas:
- `Save(layerId)` - calls `canvas.Save()`
- `Restore(layerId)` - calls `canvas.Restore()`
- `SetMatrix(layerId, matrix)` - calls `canvas.SetMatrix(matrix)`
- `DrawPolygon(...)`, `DrawText(...)`, etc. - just draw, NO transform logic

**Architecture:**
1. **Decoder** parses binary protocol, calls callback methods
2. **Callback** (RenderingCallbackV2) applies operations to layer canvas
3. **Draw methods in callback do NOT handle transforms** - canvas already has matrix set

**Transform flow:**
```
Protocol: SetContext(Offset=50,50) → Decoder calls callback.SetMatrix(layerId, matrix)
Protocol: DrawPolygon(points)      → Decoder calls callback.DrawPolygon(layerId, points, context)
                                     Callback just draws - canvas matrix already set
```

**DO NOT:**
- Add matrix checks in draw methods
- Call canvas.Save()/Restore() in draw methods
- Manually map points through matrices in draw methods

**The callback's draw methods should be 3-5 lines: get canvas, get paint, draw.**

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

### WebSocket Streaming (Server)
```csharp
using BlazorBlaze.Server;
using BlazorBlaze.VectorGraphics;

app.UseWebSockets();
app.MapVectorGraphicsEndpoint("/ws/stream", async (IRemoteCanvas canvas, CancellationToken ct) =>
{
    canvas.Begin();
    canvas.DrawPolygon(points, new DrawContext
    {
        Stroke = new RgbColor(255, 100, 100),
        Rotation = 45f,          // Degrees
        Scale = new SKPoint(1.5f, 1.5f),
        Offset = new SKPoint(100, 100)
    });
    await canvas.FlushAsync(ct);
});
```

### WebSocket Streaming (Client)
```csharp
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
├── BlazorBlaze/                         # Main library (WASM-compatible)
│   ├── Charts/                          # BarChart, TimeSeriesChart
│   └── VectorGraphics/                  # Encoder, Decoder, DrawContext
├── BlazorBlaze.Server/                  # Server extensions (ASP.NET Core)
│   ├── IRemoteCanvas.cs                 # Server-side canvas interface
│   ├── WebSocketRemoteCanvas.cs         # WebSocket implementation
│   └── VectorGraphicsEndpointExtensions.cs  # MapVectorGraphicsEndpoint
samples/
├── SampleApp/                           # Server (WebSocket endpoint)
└── SampleApp.Client/                    # Blazor WASM client
tests/
├── BlazorBlaze.Tests/                   # Unit tests
└── BlazorBlaze.E2ETests/                # End-to-end tests
benchmarks/
└── BlazorBlaze.Benchmarks/              # Performance benchmarks
```
