using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SkiaSharp.Views.Blazor.Internal
{
	[SupportedOSPlatform("browser")]
	internal partial class SizeWatcherInterop : JSModuleInterop
	{
		private const string ModuleName = "SizeWatcher";
		private const string JsFilename = "./_content/ModelingEvolution.SkiaSharp.Views.Blazor/SizeWatcher.js";
		private const string ObserveSymbol = "SizeWatcher.observe";
		private const string UnobserveSymbol = "SizeWatcher.unobserve";

		private readonly ElementReference htmlElement;
		private readonly string htmlElementId;
		private readonly FloatFloatActionHelper callbackHelper;

		private DotNetObjectReference<FloatFloatActionHelper>? callbackReference;

		public static async Task<SizeWatcherInterop> ImportAsync(IJSRuntime js, ElementReference element, Action<SKSize> callback)
		{
			var interop = new SizeWatcherInterop(js, element, callback);
			await interop.ImportAsync();
			await interop.StartAsync();
			return interop;
		}

		public SizeWatcherInterop(IJSRuntime js, ElementReference element, Action<SKSize> callback)
			: base(js, ModuleName, JsFilename)
		{
			htmlElement = element;
			htmlElementId = "_bl_" + element.Id;
			callbackHelper = new((x, y) => callback(new SKSize(x, y)));
		}

		protected override void OnDisposingModule() =>
			StopAsync().AsTask().Wait(0);

		public async ValueTask StartAsync()
		{
			callbackReference ??= DotNetObjectReference.Create(callbackHelper);

			await InvokeVoidAsync(ObserveSymbol, htmlElement, htmlElementId, callbackReference);
		}

		public async ValueTask StopAsync()
		{
			if (callbackReference == null)
				return;

			await InvokeVoidAsync(UnobserveSymbol, htmlElementId);

			callbackReference?.Dispose();
			callbackReference = null;
		}
	}
}
