namespace ModelingEvolution.BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Decodes raw stream data into canvas rendering commands.
/// Implementations are protocol-specific.
/// </summary>
public interface IFrameDecoder
{
    /// <summary>
    /// Decode a frame from the buffer and render to canvas.
    /// </summary>
    /// <param name="buffer">Input buffer (may contain partial data)</param>
    /// <param name="canvas">Target canvas for rendering</param>
    /// <returns>
    /// DecodeResult with frame number if complete, bytes consumed,
    /// or indication that more data is needed.
    /// </returns>
    DecodeResult Decode(ReadOnlySpan<byte> buffer, ICanvas canvas);

    /// <summary>
    /// Reset decoder state (e.g., clear delta tracking).
    /// </summary>
    void Reset();
}

/// <summary>
/// Result of a frame decode operation.
/// </summary>
public readonly record struct DecodeResult
{
    /// <summary>
    /// Whether decoding was successful (complete frame found).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of bytes consumed from the buffer.
    /// </summary>
    public int BytesConsumed { get; init; }

    /// <summary>
    /// Frame number if a complete frame was decoded.
    /// </summary>
    public ulong? FrameNumber { get; init; }

    /// <summary>
    /// Indicates more data is needed to complete decoding.
    /// </summary>
    public static DecodeResult NeedMoreData(int consumed = 0)
        => new() { Success = false, BytesConsumed = consumed };

    /// <summary>
    /// Indicates a complete frame was decoded.
    /// </summary>
    public static DecodeResult Frame(ulong frameNumber, int consumed)
        => new() { Success = true, FrameNumber = frameNumber, BytesConsumed = consumed };
}
