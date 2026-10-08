using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace SkiaSharp.Views.Blazor.Internal
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class ActionHelper
	{
		private readonly Func<Task> action;

		public ActionHelper(Func<Task> action)
		{
			this.action = action;
		}

		[JSInvokable]
		public Task Invoke() => action();
	}
}
