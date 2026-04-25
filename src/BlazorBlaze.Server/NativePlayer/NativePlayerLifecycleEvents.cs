using ModelingEvolution.EventAggregator;
using ProtoBuf;

namespace BlazorBlaze.Server.NativePlayer;

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
public record PlayerInitialized
{
    [ProtoMember(1)] public string Id { get; init; } = "";
    [ProtoMember(2)] public string Url { get; init; } = "";
    [ProtoMember(3)] public int X { get; init; }
    [ProtoMember(4)] public int Y { get; init; }
    [ProtoMember(5)] public int Width { get; init; }
    [ProtoMember(6)] public int Height { get; init; }

    public PlayerInitialized() { }

    public PlayerInitialized(string id, string url, int x, int y, int width, int height)
        => (Id, Url, X, Y, Width, Height) = (id, url, x, y, width, height);
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
public record PlayerDestroyed
{
    [ProtoMember(1)] public string Id { get; init; } = "";

    public PlayerDestroyed() { }

    public PlayerDestroyed(string id) => Id = id;
}
