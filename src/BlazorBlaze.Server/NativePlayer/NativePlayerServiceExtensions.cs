using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBlaze.Server.NativePlayer;

/// <summary>
/// DI registration extension for native player services.
/// </summary>
public static class NativePlayerServiceExtensions
{
    /// <summary>
    /// Registers IKioskDetector, INativePlayerRegistry, and IHttpContextAccessor.
    /// Apps that don't call this method pay zero overhead.
    /// </summary>
    public static IServiceCollection AddNativePlayer(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IKioskDetector, KioskDetector>();
        services.AddScoped<INativePlayerRegistry, NativePlayerRegistry>();

        return services;
    }
}
