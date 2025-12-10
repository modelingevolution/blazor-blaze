# BlazorBlaze Repository Structure

Unified Blazor UI framework supporting:
- 2D canvas engine with SkiaSharp
- Vector-graphics streaming (server-driven-ui) - **to be migrated**
- High-performance charts for streaming and batch operations - **to be migrated**

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
│       ├── Charts/                        # [Placeholder] High-performance charts
│       └── VectorGraphics/                # [Placeholder] Server-driven vector streaming
└── tests/
    ├── ModelingEvolution.BlazorBlaze.Tests/      # Unit tests (bUnit)
    └── ModelingEvolution.BlazorBlaze.E2ETests/   # E2E tests (Playwright)
```

## Namespaces

- `ModelingEvolution.BlazorBlaze` - Core 2D engine
- `ModelingEvolution.BlazorBlaze.Charts` - Chart components (future)
- `ModelingEvolution.BlazorBlaze.VectorGraphics` - Server-driven vectors (future)

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

## Migration Status

| Source | Status | Notes |
|--------|--------|-------|
| BlazorBlaze (platform) | ✅ Done | Core 2D engine extracted |
| VectorGraphics (video-streaming) | ⏳ Pending | Requires deep dive to separate UI from logic |
| PerfMon Charts (blazor-perfmon) | ⏳ Pending | Requires deep dive to separate UI from logic |
