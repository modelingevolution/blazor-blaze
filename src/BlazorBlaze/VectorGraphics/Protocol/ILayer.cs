using SkiaSharp;

namespace BlazorBlaze.VectorGraphics.Protocol;

public interface ILayer : IDisposable
{
    byte LayerId { get; }
    ICanvas Canvas { get; }
    void Clear();
    void DrawTo(SKCanvas target);
}
