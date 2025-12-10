using System.Net.WebSockets;
using ModelingEvolution.BlazorBlaze.VectorGraphics;
using ModelingEvolution.BlazorBlaze.VectorGraphics.Protocol;
using SampleApp.Client.Pages;
using SampleApp.Components;
using SkiaSharp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseWebSockets();

// WebSocket endpoint for 20K point stress test
app.Map("/ws/stress20k", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await StreamStressTestFrames(webSocket, context.RequestAborted);
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SampleApp.Client._Imports).Assembly);

app.Run();

static async Task StreamStressTestFrames(WebSocket webSocket, CancellationToken ct)
{
    const int TargetTotalPoints = 20000;
    const int PointsPerPolygon = 200;
    const int PolygonCount = TargetTotalPoints / PointsPerPolygon;
    const int CanvasWidth = 1200;
    const int CanvasHeight = 800;

    var colors = new RgbColor[]
    {
        new(255, 100, 100), new(100, 255, 100), new(100, 100, 255),
        new(255, 255, 100), new(255, 100, 255), new(100, 255, 255),
        new(255, 200, 100), new(200, 100, 255)
    };

    // Pre-allocate polygon point arrays
    var polygonPoints = new SKPoint[PolygonCount][];
    for (int i = 0; i < PolygonCount; i++)
    {
        polygonPoints[i] = new SKPoint[PointsPerPolygon];
    }

    ulong frameId = 0;
    double time = 0;

    // Grid layout for polygons
    int cols = 10;
    int rows = PolygonCount / cols;
    float cellWidth = CanvasWidth / (float)cols;
    float cellHeight = CanvasHeight / (float)rows;

    var buffer = new byte[512 * 1024]; // 512KB buffer for frame encoding

    // Use PeriodicTimer for precise 30 FPS timing
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(33));

    while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
    {
        frameId++;
        time += 0.02;

        // Generate animated polygon points
        for (int polyIdx = 0; polyIdx < PolygonCount; polyIdx++)
        {
            int col = polyIdx % cols;
            int row = polyIdx / cols;

            float centerX = (col + 0.5f) * cellWidth;
            float centerY = (row + 0.5f) * cellHeight;

            double phase = time + polyIdx * 0.3;
            float baseRadius = Math.Min(cellWidth, cellHeight) * 0.35f;
            float animatedRadius = baseRadius * (0.5f + 0.5f * (float)Math.Sin(phase));

            var points = polygonPoints[polyIdx];
            for (int i = 0; i < PointsPerPolygon; i++)
            {
                double angle = (2 * Math.PI * i / PointsPerPolygon) + phase;
                float radiusVariation = 1.0f + 0.3f * (float)Math.Sin(5 * angle + phase * 2);
                float r = animatedRadius * radiusVariation;

                float x = Math.Clamp((float)(centerX + r * Math.Cos(angle)), 0, CanvasWidth - 1);
                float y = Math.Clamp((float)(centerY + r * Math.Sin(angle)), 0, CanvasHeight - 1);

                points[i] = new SKPoint(x, y);
            }
        }

        // Encode frame using VectorGraphics binary protocol
        int offset = EncodeFrame(buffer, frameId, polygonPoints, colors);

        // Send frame via WebSocket
        await webSocket.SendAsync(
            new ArraySegment<byte>(buffer, 0, offset),
            WebSocketMessageType.Binary,
            true,
            ct);

        // Wait for next tick (precise 30 FPS)
        if (!await timer.WaitForNextTickAsync(ct))
            break;
    }
}

static int EncodeFrame(byte[] buffer, ulong frameId, SKPoint[][] polygons, RgbColor[] colors)
{
    int offset = 0;

    // Frame header
    buffer[offset++] = 0x00; // Frame type (master)
    BitConverter.TryWriteBytes(buffer.AsSpan(offset), frameId);
    offset += 8;
    buffer[offset++] = 0; // Layer ID

    // Object count (varint)
    offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), (uint)polygons.Length);

    // Encode each polygon
    for (int polyIdx = 0; polyIdx < polygons.Length; polyIdx++)
    {
        var points = polygons[polyIdx];
        var color = colors[polyIdx % colors.Length];

        // Object type: Polygon (1)
        buffer[offset++] = 0x01;

        // Point count (varint)
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), (uint)points.Length);

        // First point (absolute, zigzag encoded)
        int firstX = (int)points[0].X;
        int firstY = (int)points[0].Y;
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), firstX);
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), firstY);

        // Remaining points (delta encoded)
        int lastX = firstX;
        int lastY = firstY;
        for (int i = 1; i < points.Length; i++)
        {
            int x = (int)points[i].X;
            int y = (int)points[i].Y;
            offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), x - lastX);
            offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), y - lastY);
            lastX = x;
            lastY = y;
        }

        // Context with stroke color
        byte contextFlags = 0x01 | 0x04; // HasStroke | HasThickness
        buffer[offset++] = contextFlags;
        buffer[offset++] = color.R;
        buffer[offset++] = color.G;
        buffer[offset++] = color.B;
        buffer[offset++] = 255; // Alpha
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), 1); // Thickness
    }

    // End marker
    buffer[offset++] = 0xFF;
    buffer[offset++] = 0xFF;

    return offset;
}
