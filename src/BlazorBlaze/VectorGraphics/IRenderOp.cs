namespace BlazorBlaze.VectorGraphics;

public interface IRenderOp
{
    void Render(ICanvas canvasChannel);
    ushort Id { get; }
    
}