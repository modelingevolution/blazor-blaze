using ModelingEvolution.EventAggregator;
using ProtoBuf;

namespace BlazorBlaze.Server.NativePlayer;

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
public record OverlayChanged
{
    [ProtoMember(1)]
    public string? Id { get; init; }
    [ProtoMember(2)]
    public string Name { get; init; } = "";
    [ProtoMember(3)]
    public bool Visible { get; init; }

    public OverlayChanged() { }
    public OverlayChanged(string? id, string name, bool visible) => (Id, Name, Visible) = (id, name, visible);
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
public record StreamConnectRequested
{
    [ProtoMember(1)]
    public string? Id { get; init; }
    [ProtoMember(2)]
    public string Name { get; init; } = "";
    [ProtoMember(3)]
    public string Url { get; init; } = "";

    public StreamConnectRequested() { }
    public StreamConnectRequested(string? id, string name, string url) => (Id, Name, Url) = (id, name, url);
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
public record StreamDisconnectRequested
{
    [ProtoMember(1)]
    public string? Id { get; init; }
    [ProtoMember(2)]
    public string Name { get; init; } = "";

    public StreamDisconnectRequested() { }
    public StreamDisconnectRequested(string? id, string name) => (Id, Name) = (id, name);
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
public record PauseRequested
{
    [ProtoMember(1)]
    public string Id { get; init; } = "";

    public PauseRequested() { }
    public PauseRequested(string id) => Id = id;
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
public record ResumeRequested
{
    [ProtoMember(1)]
    public string Id { get; init; } = "";

    public ResumeRequested() { }
    public ResumeRequested(string id) => Id = id;
}

[ProtoContract]
[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
public record RefreshRequested
{
    [ProtoMember(1)]
    public string Id { get; init; } = "";

    public RefreshRequested() { }
    public RefreshRequested(string id) => Id = id;
}
