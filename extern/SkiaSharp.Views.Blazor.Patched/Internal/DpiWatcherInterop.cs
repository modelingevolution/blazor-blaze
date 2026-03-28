using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace SkiaSharp.Views.Blazor.Internal
{
	[SupportedOSPlatform("browser")]
	internal partial class DpiWatcherInterop : JSModuleInterop
	{
		private const string ModuleName = "DpiWatcher";
		private const string JsFilename = "./_content/SkiaSharp.Views.Blazor/DpiWatcher.js";
		private const string StartSymbol = "DpiWatcher.start";
		private const string StopSymbol = "DpiWatcher.stop";
		private const string GetDpiSymbol = "DpiWatcher.getDpi";

		private static DpiWatcherInterop? instance;

		private event Action<double>? callbacksEvent;
		private readonly FloatFloatActionHelper callbackHelper;

		private DotNetObjectReference<FloatFloatActionHelper>? callbackReference;

		public static async Task<DpiWatcherInterop> ImportAsync(IJSRuntime js, Action<double>? callback = null)
		{
			var interop = Get(js);
			await interop.ImportAsync();
			if (callback != null)
				await interop.SubscribeAsync(callback);
			return interop;
		}

		public static DpiWatcherInterop Get(IJSRuntime js) =>
			instance ??= new DpiWatcherInterop(js);

		private DpiWatcherInterop(IJSRuntime js)
			: base(js, ModuleName, JsFilename)
		{
			callbackHelper = new((o, n) => callbacksEvent?.Invoke((float)n));
		}

		protected override void OnDisposingModule() =>
			StopAsync().AsTask().Wait(0);

		public async ValueTask SubscribeAsync(Action<double> callback)
		{
			var shouldStart = callbacksEvent == null;

			callbacksEvent += callback;

			var dpi = shouldStart
				? await StartAsync()
				: await GetDpiAsync();

			callback(dpi);
		}

		public void Unsubscribe(Action<double> callback)
		{
			callbacksEvent -= callback;

			if (callbacksEvent == null)
				_ = StopAsync();
		}

		private async ValueTask<double> StartAsync()
		{
			callbackReference ??= DotNetObjectReference.Create(callbackHelper);

			return await InvokeAsync<double>(StartSymbol, callbackReference);
		}

		private async ValueTask StopAsync()
		{
			await InvokeVoidAsync(StopSymbol);

			callbackReference?.Dispose();
			callbackReference = null;
		}

		public ValueTask<double> GetDpiAsync() =>
			InvokeAsync<double>(GetDpiSymbol);
	}
}
