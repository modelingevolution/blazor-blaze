using ModelingEvolution.EventAggregator;
using ProtoBuf;

namespace BlazorBlaze.Server.NativePlayer;

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp)]
public record PlayerInitialized
{
    [ProtoMember(1)] public string Id { get; init; } = "";
    [ProtoMember(2)] public string Url { get; init; } = "";
    [ProtoMember(3)] public int X { get; init; }
    [ProtoMember(4)] public int Y { get; init; }
    [ProtoMember(5)] public int Width { get; init; }
    [ProtoMember(6)] public int Height { get; init; }
    [ProtoMember(7)] public int FrameWidth { get; init; }
    [ProtoMember(8)] public int FrameHeight { get; init; }
    [ProtoMember(9)] public int RoiX { get; init; }
    [ProtoMember(10)] public int RoiY { get; init; }
    [ProtoMember(11)] public int RoiWidth { get; init; }
    [ProtoMember(12)] public int RoiHeight { get; init; }

    /// <summary>
    /// MJPEG fallback URL for when <see cref="Url"/> is a host-local shm source that a remote
    /// native-player kiosk cannot resolve. Serialized as wire field <c>streamUrl</c>. Null/omitted
    /// when <see cref="Url"/> is already the MJPEG URL.
    /// </summary>
    [ProtoMember(13)] public string? StreamUrl { get; init; }

    public PlayerInitialized() { }

    public PlayerInitialized(string id, string url, int x, int y, int width, int height)
        => (Id, Url, X, Y, Width, Height) = (id, url, x, y, width, height);

    public PlayerInitialized(string id, string url, int x, int y, int width, int height,
                             int frameWidth, int frameHeight,
                             int roiX, int roiY, int roiWidth, int roiHeight,
                             string? streamUrl = null)
    {
        Id = id; Url = url; X = x; Y = y; Width = width; Height = height;
        FrameWidth = frameWidth; FrameHeight = frameHeight;
        RoiX = roiX; RoiY = roiY; RoiWidth = roiWidth; RoiHeight = roiHeight;
        StreamUrl = streamUrl;
    }
}

/// <summary>
/// Server→native zoom/pan viewport state (design §5), serialized to wire type
/// <c>viewport-changed</c>. Native applies it as one <c>cairo_matrix</c> over video + every overlay.
/// <see cref="NPanX"/>/<see cref="NPanY"/> are NORMALIZED pan (nPanX = tx/clipW, nPanY = ty/clipH).
/// Renderer-only — never re-emitted to the server.
/// </summary>
[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp)]
public record ViewportChanged
{
    [ProtoMember(1)] public string Id { get; init; } = "";
    [ProtoMember(2)] public double Scale { get; init; } = 1.0;
    [ProtoMember(3)] public double NPanX { get; init; }
    [ProtoMember(4)] public double NPanY { get; init; }

    public ViewportChanged() { }
    public ViewportChanged(string id, double scale, double nPanX, double nPanY)
        => (Id, Scale, NPanX, NPanY) = (id, scale, nPanX, nPanY);
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp)]
public record PlayerDestroyed
{
    [ProtoMember(1)] public string Id { get; init; } = "";

    public PlayerDestroyed() { }

    public PlayerDestroyed(string id) => Id = id;
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp)]
public record PlayRequested
{
    [ProtoMember(1)] public string Id { get; init; } = "";

    public PlayRequested() { }
    public PlayRequested(string id) => Id = id;
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp)]
public record LayoutChanged
{
    [ProtoMember(1)] public string Id { get; init; } = "";
    [ProtoMember(2)] public int X { get; init; }
    [ProtoMember(3)] public int Y { get; init; }
    [ProtoMember(4)] public int Width { get; init; }
    [ProtoMember(5)] public int Height { get; init; }

    public LayoutChanged() { }
    public LayoutChanged(string id, int x, int y, int width, int height)
        => (Id, X, Y, Width, Height) = (id, x, y, width, height);
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp)]
public record BackgroundColorChanged
{
    [ProtoMember(1)] public string Color { get; init; } = "";

    public BackgroundColorChanged() { }
    public BackgroundColorChanged(string color) => Color = color;
}
