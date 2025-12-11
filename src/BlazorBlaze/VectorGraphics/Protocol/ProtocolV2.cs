namespace BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Protocol v2 constants and types for stateful canvas API.
/// </summary>
public static class ProtocolV2
{
    /// <summary>
    /// End marker for frames (0xFF 0xFF).
    /// </summary>
    public const ushort EndMarker = 0xFFFF;

    /// <summary>
    /// First byte of end marker.
    /// </summary>
    public const byte EndMarkerByte1 = 0xFF;

    /// <summary>
    /// Second byte of end marker.
    /// </summary>
    public const byte EndMarkerByte2 = 0xFF;
}

/// <summary>
/// Layer frame types for keyframe compression.
/// </summary>
public enum FrameType : byte
{
    /// <summary>
    /// Clear layer canvas and redraw with operations that follow.
    /// </summary>
    Master = 0x00,

    /// <summary>
    /// Keep previous layer content unchanged (no operations follow).
    /// </summary>
    Remain = 0x01,

    /// <summary>
    /// Clear layer canvas to transparent (no operations follow).
    /// </summary>
    Clear = 0x02
}

/// <summary>
/// Operation types in protocol v2.
/// </summary>
public enum OpType : byte
{
    // Draw operations (0x01-0x0F)
    DrawPolygon = 0x01,
    DrawText = 0x02,
    DrawCircle = 0x03,
    DrawRect = 0x04,
    DrawLine = 0x05,
    DrawPath = 0x06,

    // Context operations (0x10-0x1F)
    SetContext = 0x10,
    SaveContext = 0x11,
    RestoreContext = 0x12,
    ResetContext = 0x13,

    // Reserved
    Reserved = 0xFE,

    // End marker (paired 0xFF 0xFF)
    EndMarker = 0xFF
}

/// <summary>
/// Property IDs for SetContext operation.
/// </summary>
public enum PropertyId : byte
{
    // Styling (0x01-0x0F)
    Stroke = 0x01,
    Fill = 0x02,
    Thickness = 0x03,
    FontSize = 0x04,
    FontColor = 0x05,

    // Transform (0x10-0x1F)
    Offset = 0x10,
    Rotation = 0x11,
    Scale = 0x12,
    Skew = 0x13,

    // Full matrix (0x20)
    Matrix = 0x20
}
