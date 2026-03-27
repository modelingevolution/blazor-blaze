# ModelingEvolution.SkiaSharp.Views.Blazor

Drop-in replacement for [`SkiaSharp.Views.Blazor`](https://www.nuget.org/packages/SkiaSharp.Views.Blazor) with full support for .NET 10 **WebAssembly threading** (`WasmEnableThreads`).

## Why this package?

The official `SkiaSharp.Views.Blazor` uses synchronous JS interop (`IJSInProcessRuntime`, `[JSImport]`) which **crashes at runtime** when WASM threading is enabled:

```
Cannot call synchronous C# methods.
Assertion at mono-threads-wasm.c:201, condition `<disabled>' not met
```

This package patches all JS interop to use **async** `IJSObjectReference` calls (`InvokeAsync`, `InvokeVoidAsync`) and fixes `SharedArrayBuffer` handling in the canvas rendering pipeline.

## Installation

```bash
dotnet add package ModelingEvolution.SkiaSharp.Views.Blazor
```

Replace any existing reference to `SkiaSharp.Views.Blazor` - the namespace and types are identical:

```xml
<!-- Remove this -->
<PackageReference Include="SkiaSharp.Views.Blazor" Version="3.119.1" />

<!-- Add this -->
<PackageReference Include="ModelingEvolution.SkiaSharp.Views.Blazor" Version="1.1.8" />
```

**No code changes required.** Same namespace (`SkiaSharp.Views.Blazor`), same types (`SKCanvasView`, `SKGLView`, `SKPaintSurfaceEventArgs`).

## What's patched

| Area | Official | This package |
|------|----------|-------------|
| JS module loading | `JSHost.ImportAsync` / `IJSInProcessRuntime` | `IJSObjectReference` via `IJSRuntime` |
| JS callbacks | `invokeMethod()` (sync) | `invokeMethodAsync()` (async) |
| DPI/Size watchers | Synchronous start/stop | Async `StartAsync`/`StopAsync` |
| Canvas `putImageData` | Direct `Module.HEAPU8.buffer` | Copies out of `SharedArrayBuffer` when threading enabled |
| Module access | Global `Module` variable | `getDotnetRuntime(0).Module` fallback for threaded context |
| Emscripten linker | `InterceptBrowserObjects` via globals | Same (kept for AOT compatibility) |

## Requirements

- .NET 10.0+
- SkiaSharp 3.119.1+
- Blazor WebAssembly (Server-side rendering doesn't need this patch)

## COOP/COEP Headers

WASM threading requires `SharedArrayBuffer`, which needs these response headers:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "credentialless";
    await next();
});
```

## License

MIT - same as SkiaSharp.
