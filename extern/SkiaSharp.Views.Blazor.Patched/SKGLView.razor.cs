using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SkiaSharp.Views.Blazor.Internal;

namespace SkiaSharp.Views.Blazor
{
	[SupportedOSPlatform("browser")]
	public partial class SKGLView : IDisposable
	{
		private SKHtmlCanvasInterop? interop;
		private SizeWatcherInterop? sizeWatcher;
		private DpiWatcherInterop? dpiWatcher;
		private SKHtmlCanvasInterop.GLInfo? jsGLInfo;
		private bool disposed;
		private ElementReference htmlCanvas;

		private const int ResourceCacheBytes = 256 * 1024 * 1024; // 256 MB
		private const SKColorType colorType = SKColorType.Rgba8888;
		private const GRSurfaceOrigin surfaceOrigin = GRSurfaceOrigin.BottomLeft;

		private GRContext? context;
		private GRGlInterface? glInterface;
		private GRBackendRenderTarget? renderTarget;
		private SKSize renderTargetSize;
		private SKSurface? surface;
		private SKCanvas? canvas;
		private bool enableRenderLoop;
		private bool ignorePixelScaling;
		private double dpi;
		private SKSize canvasSize;

		[Inject]
		IJSRuntime JS { get; set; } = null!;

		[Parameter]
		public Action<SKPaintGLSurfaceEventArgs>? OnPaintSurface { get; set; }

		[Parameter]
		public bool EnableRenderLoop
		{
			get => enableRenderLoop;
			set
			{
				if (enableRenderLoop != value)
				{
					enableRenderLoop = value;
					Invalidate();
				}
			}
		}

		[Parameter]
		public bool IgnorePixelScaling
		{
			get => ignorePixelScaling;
			set
			{
				if (ignorePixelScaling != value)
				{
					ignorePixelScaling = value;
					Invalidate();
				}
			}
		}

		[Parameter(CaptureUnmatchedValues = true)]
		public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

		public double Dpi => dpi;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (!firstRender)
				return;

			interop = await SKHtmlCanvasInterop.ImportAsync(JS, htmlCanvas, OnRenderFrame);
			if (!disposed)
				jsGLInfo = await interop.InitGLAsync();
			if (disposed || jsGLInfo == null)
			{
				ReleaseInterop();
				return;
			}

			sizeWatcher = await SizeWatcherInterop.ImportAsync(JS, htmlCanvas, OnSizeChanged);
			if (disposed)
			{
				ReleaseInterop();
				return;
			}

			dpiWatcher = await DpiWatcherInterop.ImportAsync(JS, OnDpiChanged);
			if (disposed)
				ReleaseInterop();
		}

		public void Invalidate()
		{
			if (canvasSize.Width <= 0 || canvasSize.Height <= 0 || dpi <= 0 || jsGLInfo == null || interop == null)
				return;

			_ = interop.RequestAnimationFrameAsync(EnableRenderLoop, (int)(canvasSize.Width * dpi), (int)(canvasSize.Height * dpi));
		}

		private Task OnRenderFrame()
		{
			if (canvasSize.Width <= 0 || canvasSize.Height <= 0 || dpi <= 0 || jsGLInfo == null || interop == null)
				return Task.CompletedTask;

			// create the SkiaSharp context
			if (context == null)
			{
				glInterface = GRGlInterface.Create();
				context = GRContext.CreateGl(glInterface);

				// bump the default resource cache limit
				context.SetResourceCacheLimit(ResourceCacheBytes);
			}

			// get the new surface size
			var newSize = CreateSize(out var unscaledSize);
			var info = new SKImageInfo(newSize.Width, newSize.Height, colorType);
			var userVisibleSize = IgnorePixelScaling ? unscaledSize : info.Size;

			// manage the drawing surface
			if (renderTarget == null || renderTargetSize != newSize || !renderTarget.IsValid)
			{
				// create or update the dimensions
				renderTargetSize = newSize;

				var glInfo = new GRGlFramebufferInfo(jsGLInfo.FboId, colorType.ToGlSizedFormat());

				// destroy the old surface
				surface?.Dispose();
				surface = null;
				canvas = null;

				// re-create the render target
				renderTarget?.Dispose();
				renderTarget = new GRBackendRenderTarget(newSize.Width, newSize.Height, jsGLInfo.Samples, jsGLInfo.Stencils, glInfo);
			}

			// create the surface
			if (surface == null)
			{
				surface = SKSurface.Create(context, renderTarget, surfaceOrigin, colorType);
				canvas = surface.Canvas;
			}

			using (new SKAutoCanvasRestore(canvas, true))
			{
				if (IgnorePixelScaling)
				{
					var canvas = surface.Canvas;
					canvas.Scale((float)dpi);
					canvas.Save();
				}

				// start drawing
				OnPaintSurface?.Invoke(new SKPaintGLSurfaceEventArgs(surface, renderTarget, surfaceOrigin, info.WithSize(userVisibleSize), info));
			}

			// update the control
			canvas?.Flush();
			context.Flush();
			return Task.CompletedTask;
		}

		private void OnDpiChanged(double newDpi)
		{
			dpi = newDpi;

			Invalidate();
		}

		private void OnSizeChanged(SKSize newSize)
		{
			canvasSize = newSize;

			Invalidate();
		}

		private SKSizeI CreateSize(out SKSizeI unscaledSize)
		{
			unscaledSize = SKSizeI.Empty;

			var w = canvasSize.Width;
			var h = canvasSize.Height;

			if (!IsPositive(w) || !IsPositive(h))
				return SKSizeI.Empty;

			unscaledSize = new SKSizeI((int)w, (int)h);
			return new SKSizeI((int)(w * dpi), (int)(h * dpi));

			static bool IsPositive(double value)
			{
				return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
			}
		}

		private void ReleaseInterop()
		{
			dpiWatcher?.Unsubscribe(OnDpiChanged);
			dpiWatcher = null;
			sizeWatcher?.Dispose();
			sizeWatcher = null;
			interop?.Dispose();
			interop = null;
		}

		public void Dispose()
		{
			disposed = true;
			ReleaseInterop();
		}
	}
}
