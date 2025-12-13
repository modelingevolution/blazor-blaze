namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Extension point for custom frame decoders.
/// Implementations parse binary data and emit draw operations to the stage.
/// </summary>
public interface IFrameDecoder
{
    /// <summary>
    /// Decodes binary frame data and renders to the stage.
    /// </summary>
    /// <param name="data">Binary data to decode.</param>
    /// <returns>Result indicating success, bytes consumed, and frame ID if complete.</returns>
    DecodeResultV2 Decode(ReadOnlySpan<byte> data);
}
