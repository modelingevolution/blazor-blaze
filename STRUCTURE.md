# blazor-blaze - Unified Blazor UI Framework

A consolidated Blazor UI framework combining:
- **BlazorBlaze** - 2D canvas engine with SkiaSharp (from platform)
- **VectorGraphics** - Server-driven vector streaming (from video-streaming)
- **PerfMon charts** - High-performance real-time charts (from blazor-perfmon)

## Target Framework
- `net10.0` (primary)
- `net9.0` (compatibility)

## Solution Structure

```
blazor-blaze/
├── src/
│   ├── ModelingEvolution.Blaze/                    # Core 2D engine (SkiaSharp)
│   │   ├── Core/
│   │   │   ├── BlazeEngine.cs
│   │   │   ├── Scene.cs
│   │   │   ├── Camera.cs
│   │   │   ├── HitMap.cs
│   │   │   └── ControlIdPool.cs
│   │   ├── Controls/
│   │   │   ├── Control.cs
│   │   │   ├── RootControl.cs
│   │   │   ├── ContentControl.cs
│   │   │   ├── ItemsControl.cs
│   │   │   ├── ShapeControl.cs
│   │   │   ├── RectangleControl.cs
│   │   │   ├── CircleControl.cs
│   │   │   ├── LineControl.cs
│   │   │   ├── PolygonControl.cs
│   │   │   └── BitmapControl.cs
│   │   ├── Events/
│   │   │   ├── EventManager.cs
│   │   │   ├── BubbleEvent.cs
│   │   │   ├── RootEvent.cs
│   │   │   └── Args/
│   │   │       ├── MouseEventArgs.cs
│   │   │       ├── WheelMouseEventArgs.cs
│   │   │       └── MouseOverControlChangedArgs.cs
│   │   ├── Extensions/
│   │   │   ├── ExtensionsCollection.cs
│   │   │   ├── MouseOverExtension.cs
│   │   │   ├── MouseCursorExtension.cs
│   │   │   ├── MouseZoomPanExtension.cs
│   │   │   ├── CameraMoveExtension.cs
│   │   │   ├── ControlDragExtension.cs
│   │   │   ├── DrawPolygonTool.cs
│   │   │   └── BrowserResizeExtension.cs
│   │   ├── Abstractions/
│   │   │   ├── IBlazeExtension.cs
│   │   │   ├── IBubbleEvent.cs
│   │   │   ├── IControlExtension.cs
│   │   │   ├── IControlEvent.cs
│   │   │   ├── IMouseEventArgs.cs
│   │   │   └── IRootEvent.cs
│   │   ├── Reactive/
│   │   │   ├── ObservableProperty.cs
│   │   │   └── MouseCursor.cs
│   │   ├── ValueTypes/
│   │   │   ├── AreaRange.cs
│   │   │   ├── RangeF.cs
│   │   │   └── MouseCursorType.cs
│   │   └── Components/
│   │       └── BlazorCanvas.razor              # Main Blazor component
│   │
│   ├── ModelingEvolution.Blaze.VectorGraphics/    # Server-driven vector streaming
│   │   ├── VectorGraphicsClient.cs
│   │   ├── VectorFrame.cs
│   │   ├── VectorLayer.cs
│   │   └── Serialization/
│   │       └── VectorFrameSerializer.cs
│   │
│   ├── ModelingEvolution.Blaze.Charts/            # High-performance charts
│   │   ├── BarChart.cs
│   │   ├── TimeSeriesChart.cs
│   │   ├── Renderers/
│   │   │   ├── BarChartRenderer.cs
│   │   │   └── TimeSeriesRenderer.cs
│   │   └── Components/
│   │       ├── BarChartComponent.razor
│   │       └── TimeSeriesComponent.razor
│   │
│   └── ModelingEvolution.Blaze.Server/            # Server-side streaming support
│       ├── VectorGraphicsHub.cs                   # SignalR hub for streaming
│       ├── MetricsCollector.cs
│       └── Extensions/
│           └── ServiceCollectionExtensions.cs
│
├── tests/
│   ├── ModelingEvolution.Blaze.Tests/
│   │   ├── Core/
│   │   │   ├── CameraTests.cs
│   │   │   ├── SceneTests.cs
│   │   │   └── HitMapTests.cs
│   │   ├── Controls/
│   │   │   └── ControlTests.cs
│   │   └── Extensions/
│   │       └── ExtensionTests.cs
│   │
│   ├── ModelingEvolution.Blaze.VectorGraphics.Tests/
│   │
│   ├── ModelingEvolution.Blaze.Charts.Tests/
│   │
│   └── ModelingEvolution.Blaze.E2ETests/          # Playwright E2E tests
│
├── examples/
│   └── ModelingEvolution.Blaze.Example/           # Demo application
│       ├── ModelingEvolution.Blaze.Example/       # Server
│       └── ModelingEvolution.Blaze.Example.Client/ # WASM client
│
├── blazor-blaze.sln
├── README.md
├── CLAUDE.md
├── Directory.Build.props
└── Directory.Packages.props
```

## NuGet Packages

| Package | Description |
|---------|-------------|
| `ModelingEvolution.Blaze` | Core 2D engine + controls + extensions |
| `ModelingEvolution.Blaze.VectorGraphics` | Server-driven vector frame streaming |
| `ModelingEvolution.Blaze.Charts` | High-performance charts (bar, time-series) |
| `ModelingEvolution.Blaze.Server` | Server-side streaming + metrics collection |

## Dependencies

### Core (ModelingEvolution.Blaze)
- `SkiaSharp.Views.Blazor` 3.119.1
- `Microsoft.AspNetCore.Components.Web` 10.0.0
- `ModelingEvolution.Drawing` (NuGet)

### VectorGraphics
- `ModelingEvolution.Blaze` (project reference)
- `protobuf-net` 3.2.56

### Charts
- `ModelingEvolution.Blaze` (project reference)

### Server
- `ModelingEvolution.Blaze.VectorGraphics` (project reference)
- `ModelingEvolution.Blaze.Charts` (project reference)
- `Microsoft.AspNetCore.SignalR` 10.0.0
- `MessagePack` 3.1.4

## Migration Notes

### From Platform (BlazorBlaze)
- Remove `using static MudBlazor.Colors;` from ItemsControl
- Replace `ManagedArray<T>` with `List<T>` in ControlTreeExtensions
- Namespace change: `ModelingEvolution.Platform.Client.Components.BlazorBlaze` → `ModelingEvolution.Blaze`

### From VideoStreaming (VectorGraphics)
- Namespace change: `ModelingEvolution.VideoStreaming.VectorGraphics` → `ModelingEvolution.Blaze.VectorGraphics`
- Keep protobuf serialization

### From BlazorPerfMon (Charts)
- Extract chart rendering logic
- Namespace change: `ModelingEvolution.BlazorPerfMon.Client` → `ModelingEvolution.Blaze.Charts`

## Key Design Decisions

1. **Single SkiaSharp Dependency**: All rendering goes through SkiaSharp.Views.Blazor
2. **No MudBlazor in Core**: Keep UI framework dependencies out of core engine
3. **Streaming First**: VectorGraphics designed for real-time server-driven UI
4. **Extension System**: Pluggable behaviors via IBlazeExtension
5. **Reactive Properties**: ObservableProperty<T> for data binding
