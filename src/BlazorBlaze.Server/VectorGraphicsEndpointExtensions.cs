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
    /// Maps a WebSocket endpoint for streaming vector graphics using Protocol v1 (legacy).
    /// The handler delegate can accept IRemoteCanvas and CancellationToken as special parameters,
    /// plus any DI-resolved services.
    /// </summary>
    /// <param name="app">The WebApplication.</param>
    /// <param name="pattern">The URL pattern (e.g., "/ws/graphics").</param>
    /// <param name="handler">The handler delegate. Can accept IRemoteCanvas, CancellationToken, and DI services.</param>
    /// <returns>The WebApplication for chaining.</returns>
    /// <example>
    /// app.MapVectorGraphicsEndpoint("/ws/demo", async (IRemoteCanvas canvas, IMyService svc, CancellationToken ct) =>
    /// {
    ///     canvas.Begin();
    ///     canvas.DrawCircle(100, 100, 50);
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
            using var canvas = new WebSocketRemoteCanvas(webSocket);

            var args = ResolveParameters(parameterInfo, context, canvas, null);

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

    /// <summary>
    /// Maps a WebSocket endpoint for streaming vector graphics using Protocol v2 (multi-layer, stateful context).
    /// The handler delegate can accept IRemoteCanvasV2 and CancellationToken as special parameters,
    /// plus any DI-resolved services.
    /// </summary>
    /// <param name="app">The WebApplication.</param>
    /// <param name="pattern">The URL pattern (e.g., "/ws/graphics-v2").</param>
    /// <param name="handler">The handler delegate. Can accept IRemoteCanvasV2, CancellationToken, and DI services.</param>
    /// <returns>The WebApplication for chaining.</returns>
    /// <example>
    /// app.MapVectorGraphicsEndpointV2("/ws/demo", async (IRemoteCanvasV2 canvas, CancellationToken ct) =>
    /// {
    ///     canvas.BeginFrame();
    ///     var layer = canvas.Layer(0);
    ///     layer.Master();
    ///     layer.SetStroke(RgbColor.Red);
    ///     layer.DrawCircle(100, 100, 50);
    ///     await canvas.FlushAsync(ct);
    /// });
    /// </example>
    public static WebApplication MapVectorGraphicsEndpointV2(
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

            var args = ResolveParameters(parameterInfo, context, null, canvas);

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
        RemoteCanvas,
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
        if (type == typeof(IRemoteCanvas) || type == typeof(WebSocketRemoteCanvas))
            return ParameterSource.RemoteCanvas;

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
        IRemoteCanvas? canvasV1,
        IRemoteCanvasV2? canvasV2)
    {
        var args = new object?[bindings.Length];

        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            args[i] = binding.Source switch
            {
                ParameterSource.RemoteCanvas => canvasV1,
                ParameterSource.RemoteCanvasV2 => canvasV2,
                ParameterSource.CancellationToken => context.RequestAborted,
                ParameterSource.HttpContext => context,
                ParameterSource.WebSocket => GetWebSocket(canvasV1, canvasV2),
                ParameterSource.DependencyInjection => context.RequestServices.GetRequiredService(binding.ParameterType),
                _ => throw new InvalidOperationException($"Unknown parameter source for {binding.Name}")
            };
        }

        return args;
    }

    private static WebSocket? GetWebSocket(IRemoteCanvas? canvasV1, IRemoteCanvasV2? canvasV2)
    {
        if (canvasV1 is WebSocketRemoteCanvas wsV1)
        {
            return wsV1.GetType()
                .GetField("_webSocket", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(wsV1) as WebSocket;
        }

        if (canvasV2 is WebSocketRemoteCanvasV2 wsV2)
        {
            return wsV2.GetType()
                .GetField("_webSocket", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(wsV2) as WebSocket;
        }

        return null;
    }
}
