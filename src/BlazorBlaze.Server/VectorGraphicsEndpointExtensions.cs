using System.Net.WebSockets;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBlaze.Server;

/// <summary>
/// Extension methods for registering VectorGraphics WebSocket endpoints.
/// </summary>
public static class VectorGraphicsEndpointExtensions
{
    /// <summary>
    /// Maps a WebSocket endpoint using a built-in test pattern.
    /// </summary>
    /// <param name="app">The WebApplication.</param>
    /// <param name="pattern">The URL pattern (e.g., "/ws/test").</param>
    /// <param name="patternType">The built-in test pattern to use.</param>
    /// <returns>The WebApplication for chaining.</returns>
    /// <example>
    /// app.MapVectorGraphicsEndpoint("/ws/ball", PatternType.BouncingBall);
    /// app.MapVectorGraphicsEndpoint("/ws/calibrate", PatternType.Calibration);
    /// </example>
    public static WebApplication MapVectorGraphicsEndpoint(
        this WebApplication app,
        string pattern,
        PatternType patternType)
    {
        // For patterns that support dimensions via query string: ?width=1920&height=1080
        if (patternType == PatternType.BouncingBall)
        {
            return app.MapVectorGraphicsEndpoint(pattern, async (IRemoteCanvasV2 canvas, HttpContext context, CancellationToken ct) =>
            {
                var width = GetQueryInt(context, "width", 1920);
                var height = GetQueryInt(context, "height", 1080);
                await TestPatterns.BouncingBallAsync(canvas, width, height, ct);
            });
        }

        Func<IRemoteCanvasV2, CancellationToken, Task> handler = patternType switch
        {
            PatternType.MultiLayer => TestPatterns.MultiLayerAsync,
            PatternType.Calibration => TestPatterns.CalibrationAsync,
            _ => throw new ArgumentOutOfRangeException(nameof(patternType), patternType, "Unknown pattern type")
        };

        return app.MapVectorGraphicsEndpoint(pattern, handler);
    }

    private static int GetQueryInt(HttpContext context, string key, int defaultValue)
    {
        if (context.Request.Query.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            return result;
        return defaultValue;
    }

    /// <summary>
    /// Maps a WebSocket endpoint for streaming vector graphics using Protocol v2 (multi-layer, stateful context).
    /// The handler delegate can accept IRemoteCanvasV2 and CancellationToken as special parameters,
    /// plus any DI-resolved services.
    /// </summary>
    /// <param name="app">The WebApplication.</param>
    /// <param name="pattern">The URL pattern (e.g., "/ws/graphics").</param>
    /// <param name="handler">The handler delegate. Can accept IRemoteCanvasV2, CancellationToken, and DI services.</param>
    /// <returns>The WebApplication for chaining.</returns>
    /// <example>
    /// app.MapVectorGraphicsEndpoint("/ws/demo", async (IRemoteCanvasV2 canvas, CancellationToken ct) =>
    /// {
    ///     canvas.BeginFrame();
    ///     var layer = canvas.Layer(0);
    ///     layer.Master();
    ///     layer.SetStroke(RgbColor.Red);
    ///     layer.DrawCircle(100, 100, 50);
    ///     await canvas.FlushAsync(ct);
    /// });
    /// </example>
    public static WebApplication MapVectorGraphicsEndpoint(
        this WebApplication app,
        string pattern,
        Delegate handler)
    {
        var parameterInfo = CreateParameterInfo(handler);

        app.Map(pattern, async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            using var canvas = new WebSocketRemoteCanvasV2(webSocket);

            var args = ResolveParameters(parameterInfo, context, canvas);

            var result = handler.DynamicInvoke(args);

            // If handler returns Task or ValueTask, await it
            if (result is Task task)
            {
                await task;
            }
            else if (result is ValueTask valueTask)
            {
                await valueTask;
            }
        });

        return app;
    }

    private sealed class ParameterBinding
    {
        public Type ParameterType { get; init; } = null!;
        public ParameterSource Source { get; init; }
        public string? Name { get; init; }
    }

    private enum ParameterSource
    {
        RemoteCanvasV2,
        CancellationToken,
        HttpContext,
        WebSocket,
        DependencyInjection
    }

    private static ParameterBinding[] CreateParameterInfo(Delegate handler)
    {
        var method = handler.Method;
        var parameters = method.GetParameters();
        var bindings = new ParameterBinding[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramType = param.ParameterType;

            bindings[i] = new ParameterBinding
            {
                ParameterType = paramType,
                Name = param.Name,
                Source = DetermineSource(paramType)
            };
        }

        return bindings;
    }

    private static ParameterSource DetermineSource(Type type)
    {
        if (type == typeof(IRemoteCanvasV2) || type == typeof(WebSocketRemoteCanvasV2))
            return ParameterSource.RemoteCanvasV2;

        if (type == typeof(CancellationToken))
            return ParameterSource.CancellationToken;

        if (type == typeof(HttpContext))
            return ParameterSource.HttpContext;

        if (type == typeof(WebSocket))
            return ParameterSource.WebSocket;

        return ParameterSource.DependencyInjection;
    }

    private static object?[] ResolveParameters(
        ParameterBinding[] bindings,
        HttpContext context,
        IRemoteCanvasV2 canvas)
    {
        var args = new object?[bindings.Length];

        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            args[i] = binding.Source switch
            {
                ParameterSource.RemoteCanvasV2 => canvas,
                ParameterSource.CancellationToken => context.RequestAborted,
                ParameterSource.HttpContext => context,
                ParameterSource.WebSocket => GetWebSocket(canvas),
                ParameterSource.DependencyInjection => context.RequestServices.GetRequiredService(binding.ParameterType),
                _ => throw new InvalidOperationException($"Unknown parameter source for {binding.Name}")
            };
        }

        return args;
    }

    private static WebSocket? GetWebSocket(IRemoteCanvasV2 canvas)
    {
        if (canvas is WebSocketRemoteCanvasV2 wsV2)
        {
            return wsV2.GetType()
                .GetField("_webSocket", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(wsV2) as WebSocket;
        }

        return null;
    }
}
