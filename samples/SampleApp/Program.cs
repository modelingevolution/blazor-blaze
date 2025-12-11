using System.Net.WebSockets;
using System.Numerics;
using System.Runtime.CompilerServices;
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
    const float CanvasWidth = 1200f;
    const float CanvasHeight = 800f;
    const float TwoPi = MathF.PI * 2f;
    const float AngleStep = TwoPi / PointsPerPolygon;

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

    // Pre-compute trig lookup tables for all polygon points (pure float)
    var sinTable = new float[PointsPerPolygon];
    var cosTable = new float[PointsPerPolygon];
    // Pre-compute 5x angle multiplier for radius variation
    var sin5Table = new float[PointsPerPolygon];
    var cos5Table = new float[PointsPerPolygon];
    for (int i = 0; i < PointsPerPolygon; i++)
    {
        float angle = AngleStep * i;
        sinTable[i] = MathF.Sin(angle);
        cosTable[i] = MathF.Cos(angle);
        sin5Table[i] = MathF.Sin(5f * angle);
        cos5Table[i] = MathF.Cos(5f * angle);
    }

    // Pre-compute polygon grid positions
    const int cols = 10;
    const int rows = PolygonCount / cols;
    const float cellWidth = CanvasWidth / cols;
    const float cellHeight = CanvasHeight / rows;
    float baseRadius = MathF.Min(cellWidth, cellHeight) * 0.35f;

    var centerX = new float[PolygonCount];
    var centerY = new float[PolygonCount];
    var polyPhaseOffset = new float[PolygonCount];
    for (int polyIdx = 0; polyIdx < PolygonCount; polyIdx++)
    {
        int col = polyIdx % cols;
        int row = polyIdx / cols;
        centerX[polyIdx] = (col + 0.5f) * cellWidth;
        centerY[polyIdx] = (row + 0.5f) * cellHeight;
        polyPhaseOffset[polyIdx] = polyIdx * 0.3f;
    }

    ulong frameId = 0;
    float time = 0f;
    const float TimeIncrement = 1f / 60f; // 60 FPS animation speed

    var buffer = new byte[512 * 1024]; // 512KB buffer for frame encoding

    // Use PeriodicTimer for precise 60 FPS timing (~16.67ms)
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));

    while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
    {
        frameId++;
        time += TimeIncrement;

        // Generate animated polygon points (SIMD-friendly vectorized loop)
        for (int polyIdx = 0; polyIdx < PolygonCount; polyIdx++)
        {
            float cx = centerX[polyIdx];
            float cy = centerY[polyIdx];
            float phase = time + polyPhaseOffset[polyIdx];
            float sinPhase = MathF.Sin(phase);
            float cosPhase = MathF.Cos(phase);
            float animatedRadius = baseRadius * (0.5f + 0.5f * sinPhase);

            // For radius variation: sin(5*angle + phase*2) = sin(5*angle)*cos(phase*2) + cos(5*angle)*sin(phase*2)
            float phase2 = phase * 2f;
            float sinPhase2 = MathF.Sin(phase2);
            float cosPhase2 = MathF.Cos(phase2);

            var points = polygonPoints[polyIdx];

            // Process in batches of Vector<float>.Count for SIMD
            int vectorSize = Vector<float>.Count;
            int vectorEnd = PointsPerPolygon - (PointsPerPolygon % vectorSize);

            for (int i = 0; i < vectorEnd; i += vectorSize)
            {
                // Load precomputed sin/cos values
                var sinVec = new Vector<float>(sinTable, i);
                var cosVec = new Vector<float>(cosTable, i);
                var sin5Vec = new Vector<float>(sin5Table, i);
                var cos5Vec = new Vector<float>(cos5Table, i);

                // Rotate by phase: rotatedCos = cos*cosPhase - sin*sinPhase
                var rotatedCos = cosVec * cosPhase - sinVec * sinPhase;
                var rotatedSin = sinVec * cosPhase + cosVec * sinPhase;

                // Radius variation: 1 + 0.3 * sin(5*angle + phase2)
                // sin(a+b) = sin(a)*cos(b) + cos(a)*sin(b)
                var radiusVar = Vector<float>.One + new Vector<float>(0.3f) *
                    (sin5Vec * cosPhase2 + cos5Vec * sinPhase2);
                var r = new Vector<float>(animatedRadius) * radiusVar;

                // Calculate positions
                var xVec = new Vector<float>(cx) + r * rotatedCos;
                var yVec = new Vector<float>(cy) + r * rotatedSin;

                // Clamp and store
                xVec = Vector.Max(Vector<float>.Zero, Vector.Min(xVec, new Vector<float>(CanvasWidth - 1)));
                yVec = Vector.Max(Vector<float>.Zero, Vector.Min(yVec, new Vector<float>(CanvasHeight - 1)));

                // Store results
                for (int j = 0; j < vectorSize; j++)
                {
                    points[i + j] = new SKPoint(xVec[j], yVec[j]);
                }
            }

            // Handle remaining elements
            for (int i = vectorEnd; i < PointsPerPolygon; i++)
            {
                float sin = sinTable[i];
                float cos = cosTable[i];
                float rotatedCos = cos * cosPhase - sin * sinPhase;
                float rotatedSin = sin * cosPhase + cos * sinPhase;
                float radiusVar = 1f + 0.3f * (sin5Table[i] * cosPhase2 + cos5Table[i] * sinPhase2);
                float r = animatedRadius * radiusVar;

                float x = MathF.Max(0f, MathF.Min(cx + r * rotatedCos, CanvasWidth - 1));
                float y = MathF.Max(0f, MathF.Min(cy + r * rotatedSin, CanvasHeight - 1));
                points[i] = new SKPoint(x, y);
            }
        }

        // Encode frame using VectorGraphics binary protocol
        int offset = EncodeFrame(buffer, frameId, polygonPoints, colors);

        // Wait for next tick (precise 60 FPS)
        if (!await timer.WaitForNextTickAsync(ct))
            break;

        // Send frame via WebSocket
        await webSocket.SendAsync(
            new ArraySegment<byte>(buffer, 0, offset),
            WebSocketMessageType.Binary,
            true,
            ct);
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
