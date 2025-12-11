namespace BlazorBlaze.VectorGraphics;

/// <summary>
/// Configuration options for the VectorGraphics rendering stream.
/// </summary>
public record VectorGraphicsOptions
{
    /// <summary>
    /// Default options using binary protocol.
    /// </summary>
    public static readonly VectorGraphicsOptions Default = new();

    /// <summary>
    /// Use binary protocol (improved) instead of protobuf (legacy).
    /// Binary protocol uses varint+zigzag encoding for efficient network transfer.
    /// </summary>
    public bool UseBinaryProtocol { get; init; } = true;

    /// <summary>
    /// Layers to render (null = all layers).
    /// </summary>
    public int[]? FilteredLayers { get; init; }

    /// <summary>
    /// Maximum buffer size in bytes. Default is 8MB.
    /// </summary>
    public int MaxBufferSize { get; init; } = 8 * 1024 * 1024;
}
