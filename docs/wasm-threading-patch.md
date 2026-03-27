# WASM Threading Patch for SkiaSharp.Views.Blazor

## Summary

Patched SkiaSharp.Views.Blazor 3.119.1 to support `WasmEnableThreads` (Blazor WASM multi-threading).
Added GC monitoring counters and FPS metrics reporting to the stress test page.

## Problem

SkiaSharp.Views.Blazor 3.119.1 uses synchronous JS→C# interop via `[JSImport]` with `Action` callbacks.
When `WasmEnableThreads` is enabled in Blazor WASM, synchronous calls from JS to .NET are forbidden —
the runtime throws: `"Cannot call synchronous C# methods"`.

This affects three interop classes: `SKHtmlCanvasInterop`, `SizeWatcherInterop`, `DpiWatcherInterop`.

Additionally, with threading enabled:
- The WASM heap becomes a `SharedArrayBuffer`, which browsers reject in `ImageData` constructors
- The global `Module` object is no longer available; it must be accessed via `getDotnetRuntime(0).Module`

## Architecture of the Fix

### Approach: DotNetObjectReference + invokeMethodAsync

Replaced all synchronous `[JSImport]` / `JSHost.ImportAsync` / `JSObject` patterns with:
- **C# side:** `IJSObjectReference` obtained via `js.InvokeAsync<IJSObjectReference>("import", url)`
- **C# side:** `DotNetObjectReference<T>` for callbacks (ActionHelper, FloatFloatActionHelper)
- **JS side:** `callback.invokeMethodAsync('Invoke')` instead of `callback.invokeMethod('Invoke')`

This makes all JS→C# communication async, which is compatible with WASM threading.

## Changed Files

### SkiaSharp Submodule (extern/SkiaSharp) — C# Interop

| File | Change |
|------|--------|
| `Internal/JSModuleInterop.cs` | Replaced `JSObject`/`JSHost.ImportAsync` with `IJSObjectReference`/`js.InvokeAsync`. All `Invoke`/`Invoke<T>` → `InvokeVoidAsync`/`InvokeAsync<T>`. Removed `#if NET7_0_OR_GREATER` branches. |
| `Internal/SKHtmlCanvasInterop.cs` | All methods async: `InitGLAsync`, `InitRasterAsync`, `DeinitAsync`, `RequestAnimationFrameAsync`, `PutImageDataAsync`. Removed `[JSImport]` paths. Removed `InterceptBrowserObjects` DllImport (was .NET 7 workaround, caused linker error on .NET 10). |
| `Internal/SizeWatcherInterop.cs` | `Start`→`StartAsync`, `Stop`→`StopAsync`. Uses `DotNetObjectReference<FloatFloatActionHelper>`. Removed `[JSImport]` paths. |
| `Internal/DpiWatcherInterop.cs` | `Subscribe`→`SubscribeAsync`, `Start`→`StartAsync`, `Stop`→`StopAsync`, `GetDpi`→`GetDpiAsync`. Removed `[JSImport]` paths. |
| `Internal/ActionHelper.cs` | Removed `#if !NET7_0_OR_GREATER` guard — now always compiled. |
| `Internal/FloatFloatActionHelper.cs` | Removed `#if !NET7_0_OR_GREATER` guard — now always compiled. |
| `SKCanvasView.razor.cs` | Updated to call async interop methods (`InitRasterAsync`, `RequestAnimationFrameAsync`, `PutImageDataAsync`). |
| `SKGLView.razor.cs` | Updated to call async interop methods (`InitGLAsync`, `RequestAnimationFrameAsync`). |
| `wwwroot/SKHtmlCanvas.ts` | `requestAnimationFrame` callback made `async`, `invokeMethod`→`invokeMethodAsync` for DotNetObjectReference path. |
| `wwwroot/DpiWatcher.ts` | Callback type updated to allow `Promise<void>`. |
| `wwwroot/SizeWatcher.ts` | Callback type updated to allow `Promise<void>`. |

### Patched JS Files (extern/SkiaSharp.Views.Blazor.Patched/wwwroot)

Pre-compiled JS files served as static web assets (avoids TypeScript MSBuild dependency):

| File | Change |
|------|--------|
| `SKHtmlCanvas.js` | `invokeMethod`→`invokeMethodAsync`. `requestAnimationFrame` callback is `async`. `getModule()` falls back to `getDotnetRuntime(0).Module` for .NET 10 threading. `putImageData` copies pixel data out of `SharedArrayBuffer` before creating `ImageData`. |
| `DpiWatcher.js` | `invokeMethod`→`invokeMethodAsync` in `update()`. |
| `SizeWatcher.js` | `invokeMethod`→`invokeMethodAsync` in `invoke()`. |

### Patched Project (extern/SkiaSharp.Views.Blazor.Patched)

| File | Purpose |
|------|---------|
| `SkiaSharp.Views.Blazor.Patched.csproj` | Razor class library that compiles C# from the SkiaSharp submodule and bundles patched JS from `wwwroot/`. Assembly name = `SkiaSharp.Views.Blazor` (drop-in replacement). Targets `net10.0`. |

### SampleApp Changes

| File | Change |
|------|--------|
| `SampleApp.Client/SampleApp.Client.csproj` | Added `<WasmEnableThreads>true</WasmEnableThreads>`. Changed SkiaSharp.Views.Blazor from NuGet to patched project reference. |
| `SampleApp.Client/Program.cs` | Added `HttpClient` registration for WASM client (needed for metrics endpoint). |
| `SampleApp.Client/Pages/StressTest.razor` | Added GC counters (Gen0/1/2, pause duration, heap size) to UI. Added metrics reporting timer (POST to `/api/stress-metrics` every 10s). |
| `SampleApp/Program.cs` | Added COOP/COEP headers middleware (required for `SharedArrayBuffer`). Added `/api/stress-metrics` REST endpoint (saves CSV). Added server-side `HttpClient` registration. |
| `SampleApp/Components/Layout/ReconnectModal.razor` | Moved `<script>` after `<dialog>` element. Changed from `type="module"` to `@Assets["..."]` to avoid importmap integrity bug. |
| `SampleApp/run.sh` | Fixed to run from `publish/` directory (content root must match publish output for `MapStaticAssets` to find compressed files). |
| `src/BlazorBlaze/BlazorBlaze.csproj` | Changed SkiaSharp.Views.Blazor from NuGet to patched project reference. |
| `Directory.Packages.props` | Added `SkiaSharp.NativeAssets.WebAssembly` and `Microsoft.TypeScript.MSBuild` versions. |

## Key Technical Details

### Why AOT is Required

With `WasmEnableThreads`, the Blazor framework's own `updateRootComponents` JS→C# call is synchronous.
AOT compilation resolves this. Without AOT (interpreter mode), the app crashes on startup with
`"Cannot call synchronous C# methods"` from `blazor.web.js`.

### COOP/COEP Headers

`SharedArrayBuffer` requires these HTTP headers on the document:
```
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: credentialless
```

### SkiaSharp Native Libraries

SkiaSharp 3.119.1 ships both `st` (single-threaded) and `mt` (multi-threaded) WASM native libraries
via `SkiaSharp.NativeAssets.WebAssembly`. The MSBuild property `WasmEnableThreads` automatically selects
the `mt` variant — no manual configuration needed.

### MapStaticAssets Content Root Bug

When running `dotnet ./publish/SampleApp.dll` from the project directory (not the publish directory),
the content root points to the project directory. `MapStaticAssets` then can't find compressed static
files (`.gz`, `.br`) and returns `Content-Length: 0`. **Must run from the publish directory.**

## NuGet Publishing Decision

Currently, both `BlazorBlaze.csproj` and `SampleApp.Client.csproj` use `<ProjectReference>` to the
patched project. For NuGet publishing, there are two options:

### Option A: Publish as separate NuGet package (Recommended)

Publish the patched library as `ModelingEvolution.SkiaSharp.Views.Blazor` (different package ID to
avoid conflict with the official SkiaSharp package):

1. Add NuGet metadata to `SkiaSharp.Views.Blazor.Patched.csproj`:
   - `<PackageId>ModelingEvolution.SkiaSharp.Views.Blazor</PackageId>`
   - `<Version>3.119.1-threading.1</Version>` (tracks upstream version + our patch)
   - `<Description>SkiaSharp.Views.Blazor patched for WasmEnableThreads support</Description>`
2. Add to `release.sh` / GitHub Actions publish workflow
3. Update `BlazorBlaze.csproj` to reference this package instead of project reference
4. Consumers add `ModelingEvolution.SkiaSharp.Views.Blazor` instead of `SkiaSharp.Views.Blazor`

### Option B: Bundle into BlazorBlaze NuGet

Include the patched SkiaSharp.Views.Blazor source directly in the BlazorBlaze library:

- Pro: Consumers just reference `BlazorBlaze` — no extra package
- Con: Tight coupling, harder to update when upstream SkiaSharp releases a fix
- Con: Assembly name collision if consumer also references official SkiaSharp.Views.Blazor

### Option C: Wait for upstream fix

Monitor the SkiaSharp repo for WASM threading support. When upstream fixes the issue,
revert to using the official NuGet package. The TypeScript changes have already been made
in our fork and could be submitted as a PR to upstream.

## Testing

Verified with Playwright (Chrome 145):
- `/stress` — 62.5 FPS, 2.6 MB/s WebSocket, GC counters updating, zero console errors
- `/canvas` — Raster canvas rendering shapes correctly, zero console errors
- `crossOriginIsolated: true`, `SharedArrayBuffer` available
