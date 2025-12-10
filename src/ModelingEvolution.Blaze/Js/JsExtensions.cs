using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ModelingEvolution.Blaze.Js
{
    public static class JsExtensions
    {
        public static async Task<ElementSize> GetBoundingClientRect(this IJSRuntime js, ElementReference r)
        {
            return await js.InvokeAsync<ElementSize>("getElementBoundingClientRect", [r]);
        }

        public static async Task<float> DevicePixelRatio(this IJSRuntime js)
        {
            return await js.InvokeAsync<float>("getDevicePixelRatio");
        }
    }
    public record ElementSize
    {
        public double Width { get; init; }
        public double Height { get; init; }
        public double OffsetWidth { get; init; }
        public double OffsetHeight { get; init; }
        public double ScrollWidth { get; init; }
        public double ScrollHeight { get; init; }
        public DOMRect BoundingClientRect { get; init; }
    }
    public record DOMRect
    {
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public double Top { get; init; }
        public double Right { get; init; }
        public double Bottom { get; init; }
        public double Left { get; init; }
    }
}
