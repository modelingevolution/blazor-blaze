using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SkiaSharp.Views.Blazor.Internal
{
	[SupportedOSPlatform("browser")]
	internal partial class SKHtmlCanvasInterop : JSModuleInterop
	{
		private const string ModuleName = "SKHtmlCanvas";
		private const string JsFilename = "./_content/ModelingEvolution.SkiaSharp.Views.Blazor/SKHtmlCanvas.js";
		private const string InitGLSymbol = "SKHtmlCanvas.initGL";
		private const string InitRasterSymbol = "SKHtmlCanvas.initRaster";
		private const string DeinitSymbol = "SKHtmlCanvas.deinit";
		private const string RequestAnimationFrameSymbol = "SKHtmlCanvas.requestAnimationFrame";
		private const string PutImageDataSymbol = "SKHtmlCanvas.putImageData";

		private readonly ElementReference htmlCanvas;
		private readonly string htmlElementId;
		private readonly ActionHelper callbackHelper;

		private DotNetObjectReference<ActionHelper>? callbackReference;

		public static async Task<SKHtmlCanvasInterop> ImportAsync(IJSRuntime js, ElementReference element, Action callback)
		{
			var interop = new SKHtmlCanvasInterop(js, element, callback);
			await interop.ImportAsync();
			return interop;
		}

		public SKHtmlCanvasInterop(IJSRuntime js, ElementReference element, Action renderFrameCallback)
			: base(js, ModuleName, JsFilename)
		{
			htmlCanvas = element;
			htmlElementId = "_bl_" + element.Id;

			callbackHelper = new(renderFrameCallback);
		}

		protected override void OnDisposingModule() =>
			DeinitAsync().AsTask().Wait(0);

		public async ValueTask<GLInfo> InitGLAsync()
		{
			if (callbackReference != null)
				throw new InvalidOperationException("Unable to initialize the same canvas more than once.");

			Init();

			callbackReference = DotNetObjectReference.Create(callbackHelper);

			return await InvokeAsync<GLInfo>(InitGLSymbol, htmlCanvas, htmlElementId, callbackReference);
		}

		public async ValueTask<bool> InitRasterAsync()
		{
			if (callbackReference != null)
				throw new InvalidOperationException("Unable to initialize the same canvas more than once.");

			Init();

			callbackReference = DotNetObjectReference.Create(callbackHelper);

			return await InvokeAsync<bool>(InitRasterSymbol, htmlCanvas, htmlElementId, callbackReference);
		}

		public async ValueTask DeinitAsync()
		{
			if (callbackReference == null)
				return;

			await InvokeVoidAsync(DeinitSymbol, htmlElementId);

			callbackReference?.Dispose();
		}

		public ValueTask RequestAnimationFrameAsync(bool enableRenderLoop, int rawWidth, int rawHeight) =>
			InvokeVoidAsync(RequestAnimationFrameSymbol, htmlElementId, enableRenderLoop, rawWidth, rawHeight);

		public ValueTask PutImageDataAsync(IntPtr intPtr, SKSizeI rawSize) =>
			InvokeVoidAsync(PutImageDataSymbol, htmlElementId, intPtr.ToInt64(), rawSize.Width, rawSize.Height);

		public record GLInfo(int ContextId, uint FboId, int Stencils, int Samples, int Depth);

		static void Init()
		{
			// InterceptBrowserObjects was a workaround for https://github.com/dotnet/runtime/issues/76077
			// No longer needed on .NET 10
		}
	}
}
