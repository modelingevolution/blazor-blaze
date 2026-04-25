using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelingEvolution.EventAggregator;

namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// DI registration extension for native player services.
/// </summary>
public static class NativePlayerServiceExtensions
{
    /// <summary>
    /// Registers IKioskDetector, IHttpContextAccessor, and IEventAggregator (with NullForwarder).
    /// Call AddEventAggregatorBlazor().AsNativeCpp() in the host app to wire full EA transport.
    /// Apps that don't call this method pay zero overhead.
    /// </summary>
    public static IServiceCollection AddNativePlayer(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IKioskDetector, KioskDetector>();
        services.AddEventAggregator();

        return services;
    }
}
