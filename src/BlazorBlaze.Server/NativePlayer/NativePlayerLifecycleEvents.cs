using ModelingEvolution.EventAggregator;
using ModelingEvolution.EventAggregator.Blazor;

namespace BlazorBlaze.Server.NativePlayer;

[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
[NativeCppEventName("init")]
public record PlayerInitialized
{
    public string Id { get; init; } = "";
    public string Url { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public PlayerInitialized() { }

    public PlayerInitialized(string id, string url, int x, int y, int width, int height)
        => (Id, Url, X, Y, Width, Height) = (id, url, x, y, width, height);
}

[SubscriptionScope(SubscriptionScopeFlags.NativeCpp | SubscriptionScopeFlags.ServerSide)]
[NativeCppEventName("destroy-player")]
public record PlayerDestroyed
{
    public string Id { get; init; } = "";

    public PlayerDestroyed() { }

    public PlayerDestroyed(string id) => Id = id;
}
