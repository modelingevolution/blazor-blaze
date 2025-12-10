using System.Drawing;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ModelingEvolution.Blaze.Js;
using SkiaSharp;

namespace ModelingEvolution.Blaze
{
    public class BrowserResizeExtension(IJSRuntime _js, ElementReference containerRef) : IEngineExtension
    {
        private BlazeEngine? _engine;
        public ElementReference ContainerRef { get; set; } = containerRef;

        public async Task<System.Drawing.Size> Refresh(bool fit = false)
        {
            var boundingClientRect = await _js.GetBoundingClientRect(ContainerRef);
            var devicePixelRatio = await _js.DevicePixelRatio();
            if(_engine != null)
            _engine.EventManager.QueueAction(() =>
            {
                _engine.Scene.Camera.BrowserControlSize = new SKSize((float)boundingClientRect.Width, (float)boundingClientRect.Height);
                _engine.Scene.Camera.DevicePixelRatio = devicePixelRatio;
                if(fit)
                    _engine.Scene.Fit();
            });
            return new Size((int)boundingClientRect.Width, (int)boundingClientRect.Height);
        }
        public void Bind(BlazeEngine engine)
        {
            _engine = engine;
        }

        public void Unbind(BlazeEngine engine)
        {
            
        }
    }
}
