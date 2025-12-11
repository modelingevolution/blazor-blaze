# BlazorBlaze Repository Structure

Unified Blazor UI framework supporting:
- 2D canvas engine with SkiaSharp
- Vector-graphics streaming (server-driven-ui)
- High-performance charts for streaming and batch operations

## Project Layout

```
blazor-blaze/
├── src/
│   ├── BlazorBlaze/                       # Main NuGet package (WASM-compatible)
│   │   ├── Abstractions/                  # Interfaces (IBlazeExtension, IBubbleEvent, etc.)
│   │   ├── Collections/                   # Performance collections (ManagedArray<T>)
│   │   ├── Controls/                      # Control hierarchy (Control, ContentControl, ItemsControl)
│   │   ├── EventArgs/                     # Mouse, Wheel, Keyboard events
│   │   ├── Extensions/                    # Pluggable behaviors (drag, zoom, pan, draw tools)
│   │   ├── Js/                            # JavaScript interop
│   │   ├── ValueTypes/                    # Value types (AreaRange, MouseCursorType, RangeF)
│   │   ├── Charts/                        # High-performance generic charts
│   │   └── VectorGraphics/                # Encoder, Decoder, DrawContext, Protocol
│   └── BlazorBlaze.Server/                # Server NuGet package (ASP.NET Core)
│       ├── IRemoteCanvas.cs               # Server-side canvas interface
│       ├── WebSocketRemoteCanvas.cs       # WebSocket implementation
│       └── VectorGraphicsEndpointExtensions.cs  # MapVectorGraphicsEndpoint
├── samples/
│   ├── SampleApp/                         # ASP.NET Core server
│   └── SampleApp.Client/                  # Blazor WASM client
└── tests/
    ├── BlazorBlaze.Tests/                 # Unit tests (bUnit)
    └── BlazorBlaze.E2ETests/              # E2E tests (Playwright)
```

## Namespaces

- `BlazorBlaze` - Core 2D engine
- `BlazorBlaze.Charts` - Generic chart components
- `BlazorBlaze.VectorGraphics` - Encoder, Decoder, DrawContext
- `BlazorBlaze.Server` - Server-side streaming (IRemoteCanvas, MapVectorGraphicsEndpoint)

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

## Migration Status

| Source | Status | Files | Notes |
|--------|--------|-------|-------|
| BlazorBlaze (platform) | ✅ Done | ~60 | Core 2D engine |
| VectorGraphics | ✅ Done | ~15 | Binary protocol with transforms |
| BlazorBlaze.Server | ✅ Done | 3 | IRemoteCanvas, MapVectorGraphicsEndpoint |
| Charts | ✅ Done | 9 | BarChart, TimeSeriesChart |
