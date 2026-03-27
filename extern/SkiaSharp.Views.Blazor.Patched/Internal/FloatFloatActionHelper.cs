using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace SkiaSharp.Views.Blazor.Internal
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class FloatFloatActionHelper
	{
		private readonly Action<float, float> action;

		public FloatFloatActionHelper(Action<float, float> action)
		{
			this.action = action;
		}

		[JSInvokable]
		public Task Invoke(float width, float height)
		{
			action?.Invoke(width, height);
			return Task.CompletedTask;
		}
	}
}
