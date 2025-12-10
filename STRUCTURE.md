# BlazorBlaze Repository Structure

Unified Blazor UI framework supporting:
- 2D canvas engine with SkiaSharp
- Vector-graphics streaming (server-driven-ui)
- High-performance charts for streaming and batch operations

## Project Layout

```
blazor-blaze/
├── src/
│   └── ModelingEvolution.BlazorBlaze/    # Single unified NuGet package
│       ├── Abstractions/                  # Interfaces (IBlazeExtension, IBubbleEvent, etc.)
│       ├── Collections/                   # Performance collections (ManagedArray<T>)
│       ├── Controls/                      # Control hierarchy (Control, ContentControl, ItemsControl)
│       ├── EventArgs/                     # Mouse, Wheel, Keyboard events
│       ├── Extensions/                    # Pluggable behaviors (drag, zoom, pan, draw tools)
│       ├── Js/                            # JavaScript interop
│       ├── ValueTypes/                    # Value types (AreaRange, MouseCursorType, RangeF)
│       ├── Charts/                        # High-performance generic charts
│       └── VectorGraphics/                # Server-driven vector streaming
└── tests/
    ├── ModelingEvolution.BlazorBlaze.Tests/      # Unit tests (bUnit)
    └── ModelingEvolution.BlazorBlaze.E2ETests/   # E2E tests (Playwright)
```

## Namespaces

- `ModelingEvolution.BlazorBlaze` - Core 2D engine
- `ModelingEvolution.BlazorBlaze.Charts` - Generic chart components
- `ModelingEvolution.BlazorBlaze.VectorGraphics` - Server-driven vector streaming

## Key Components

### Core Engine
- **Scene** - Root container with z-index layering and HitMap
- **Camera** - Zoom/pan with coordinate transformation
- **BlazeEngine** - Engine orchestrator with extension system
- **BlazorCanvas.razor** - Blazor component wrapping SkiaSharp canvas

### Control System
- **Control** - Base class with reactive properties
- **ContentControl** - Single child container
- **ItemsControl** - Multiple children container
- **ShapeControl** - Base for geometric shapes (Circle, Rectangle, Line, Polygon)

### Performance
- **ManagedArray<T>** - ArrayPool-backed collection to avoid GC pressure
- **HitMap** - Efficient hit testing via color-coded rendering

## Dependencies

- SkiaSharp.Views.Blazor (2D rendering)
- ModelingEvolution.Drawing (geometry primitives)
- System.Reactive (reactive property system)
- Microsoft.AspNetCore.Components.Web (Blazor)

---

## Migration Plan

### VectorGraphics (from video-streaming) - ALL 25 files

Move entire streaming canvas infrastructure:
- `ICanvas.cs`, `SkiaCanvas.cs` - Canvas abstraction and SkiaSharp implementation
- `RemoteCanvas.cs`, `StreamingCanvasEngine.cs` - Server-driven rendering
- `ProtoStreamClient.cs`, `Serializer.cs`, `WebSocketFrameWriter.cs` - Protocol/streaming
- `Draw.cs`, `DrawContext.cs` - Render operations
- `Polygon.cs`, `Text.cs`, `Vector.cs` - Render items
- `RgbColor.cs`, `HsvColor.cs` - Color types
- All supporting files (buffers, extensions, etc.)

**Note:** Consider refactoring to use MessagePack instead of Protobuf to align with segmentation/keypoints protocols.

### Charts (from blazor-perfmon) - 9 generic files

Move only the **generic chart infrastructure** (fully parameterized, no domain knowledge):

| File | Description |
|------|-------------|
| `IChart.cs` | Chart interface |
| `ChartBase.cs` | Abstract base with SKCanvas infrastructure |
| `BarChart.cs` | Generic horizontal bar chart (configurable title, labels, colors, units) |
| `TimeSeriesChart.cs` | Generic time-series line chart (configurable series, timestamps, scaling) |
| `TimeSeriesF.cs` | Generic time series data structure |
| `SeriesData.cs` | Generic series wrapper |
| `Brushes.cs` | Reusable SKPaint definitions |
| `ChartColors.cs` | Color palette |
| `ChartStyles.cs` | Fonts, paints, styling infrastructure |

**DO NOT move** (domain-specific, stay in BlazorPerfMon):
- `CpuBarChart.cs`, `GpuBarChart.cs`, `TemperatureBarChart.cs` - Use MetricSample
- `NetworkChart.cs`, `DiskChart.cs`, `ComputeLoadChart.cs` - Use domain models
- `DockerContainersChart.cs`, `TitleFormatters.cs` - Domain-specific
- `CpuGraphRenderer.cs`, `TimeSeriesGraphRenderer.cs` - Redundant legacy renderers

### BlazorPerfMon Refactoring (future)

After migration, BlazorPerfMon will:
1. Add dependency on `ModelingEvolution.BlazorBlaze`
2. Replace redundant renderers (`CpuGraphRenderer`, `TimeSeriesGraphRenderer`) with generic `BarChart`/`TimeSeriesChart`
3. Keep domain-specific wrappers that extract data from `MetricSample` and configure the generic charts

Example replacement:
```csharp
// Before (redundant hardcoded renderer):
cpuGraphRenderer.Render(canvas, bounds, cpuLoads);

// After (using generic library chart):
barChart.SetData(
    title: "CPU Cores",
    labels: Enumerable.Range(0, cpuCount).Select(i => $"CPU{i}"),
    values: cpuLoads,
    colorMapper: _ => ChartStyles.CpuBarColor
);
barChart.Render(canvas, bounds);
```

---

## Migration Status

| Source | Status | Files | Notes |
|--------|--------|-------|-------|
| BlazorBlaze (platform) | ✅ Done | ~60 | Core 2D engine extracted |
| VectorGraphics (video-streaming) | ⏳ Pending | 25 | All files - streaming infrastructure |
| Charts (blazor-perfmon) | ⏳ Pending | 9 | Generic charts only |
