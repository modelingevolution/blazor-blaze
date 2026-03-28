using System;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using Microsoft.JSInterop;

namespace SkiaSharp.Views.Blazor.Internal
{
	[SupportedOSPlatform("browser")]
	internal partial class JSModuleInterop : IDisposable
	{
		private readonly Task<IJSObjectReference> moduleTask;
		private IJSObjectReference? module;

		public JSModuleInterop(IJSRuntime js, string moduleName, string moduleUrl)
		{
			moduleTask = js.InvokeAsync<IJSObjectReference>("import", moduleUrl).AsTask();
		}

		public async Task ImportAsync()
		{
			module = await moduleTask;
		}

		public void Dispose()
		{
			OnDisposingModule();
		}

		protected IJSObjectReference Module =>
			module ?? throw new InvalidOperationException("Make sure to run ImportAsync() first.");

		protected ValueTask InvokeVoidAsync(string identifier, params object?[]? args) =>
			Module.InvokeVoidAsync(identifier, args);

		protected ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[]? args) =>
			Module.InvokeAsync<TValue>(identifier, args);

		protected virtual void OnDisposingModule() { }
	}
}
