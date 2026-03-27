using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace SkiaSharp.Views.Blazor.Internal
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class ActionHelper
	{
		private readonly Action action;

		public ActionHelper(Action action)
		{
			this.action = action;
		}

		[JSInvokable]
		public Task Invoke()
		{
			action?.Invoke();
			return Task.CompletedTask;
		}
	}
}
