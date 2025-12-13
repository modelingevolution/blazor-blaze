using BlazorBlaze.ValueTypes;

namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Stage interface for multi-layer canvas management.
/// Provides access to layer canvases via indexer and frame lifecycle events.
/// </summary>
public interface IStage
{
    /// <summary>
    /// Gets-or-creates the canvas for the specified layer.
    /// </summary>
    ICanvas this[byte layerId] { get; }

    /// <summary>
    /// Called when a new frame starts.
    /// </summary>
    void OnFrameStart(ulong frameId);

    /// <summary>
    /// Called when the frame ends.
    /// </summary>
    void OnFrameEnd();

    /// <summary>
    /// Clears the specified layer (used for Master and Clear frame types).
    /// </summary>
    void Remain(byte layerId);
    void Clear(byte layerId);

    /// <summary>
    /// This shall be called by renderer thread, to get a copy of the lastest complete frame.
    /// </summary>
    /// <param name="copy"></param>
    /// <returns></returns>
    bool TryCopyFrame(out RefArray<Lease<ILayer>>? copy);
}



