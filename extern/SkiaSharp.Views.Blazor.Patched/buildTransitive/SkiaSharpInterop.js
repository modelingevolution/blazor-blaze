// Workaround for https://github.com/dotnet/runtime/issues/76077
// Provides InterceptBrowserObjects for Emscripten linker compatibility.
// On .NET 10 with WasmEnableThreads, this is a no-op — the patched
// SKHtmlCanvas.js retrieves Module via getDotnetRuntime() instead.

var SkiaSharpInterop = {
	$SkiaSharpLibrary: {
		internal_func: function () {
		}
	},
    InterceptBrowserObjects: function () {
		globalThis.SkiaSharpGL = GL
        globalThis.SkiaSharpModule = Module
    }
}

autoAddDeps(SkiaSharpInterop, '$SkiaSharpLibrary')
mergeInto(LibraryManager.library, SkiaSharpInterop)
