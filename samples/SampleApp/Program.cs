using BlazorBlaze.Server;
using BlazorBlaze.VectorGraphics;
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

// WebSocket endpoint for 20K point stress test using Protocol v2 (multi-layer, stateful context)
app.MapVectorGraphicsEndpointV2("/ws/stress20k", StreamStressTestFramesV2);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SampleApp.Client._Imports).Assembly);

app.Run();

static async Task StreamStressTestFramesV2(IRemoteCanvasV2 canvas, CancellationToken ct)
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

    // Create STATIC polygon points ONCE - centered at origin with star shape
    // Animation will be done via transform matrix (Rotation, Scale, Offset)
    var polygonPoints = new SKPoint[PolygonCount][];
    for (int polyIdx = 0; polyIdx < PolygonCount; polyIdx++)
    {
        var points = new SKPoint[PointsPerPolygon];
        for (int i = 0; i < PointsPerPolygon; i++)
        {
            float angle = AngleStep * i;
            // Star shape: radius varies with 5x frequency
            float radiusVar = 1f + 0.3f * MathF.Sin(5f * angle);
            float r = baseRadius * radiusVar;
            // Points centered at origin - will be translated via Offset transform
            points[i] = new SKPoint(r * MathF.Cos(angle), r * MathF.Sin(angle));
        }
        polygonPoints[polyIdx] = points;
    }

    float time = 0f;
    const float TimeIncrement = 1f / 60f; // 60 FPS animation speed

    // Use PeriodicTimer for precise 60 FPS timing (~16.67ms)
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));

    while (!ct.IsCancellationRequested)
    {
        time += TimeIncrement;

        // Begin new frame
        canvas.BeginFrame();

        // Get layer 0 for drawing (single layer for this stress test)
        var layer = canvas.Layer(0);
        layer.Master(); // Clear and redraw

        // Set common stroke thickness once
        layer.SetThickness(1);

        // Draw each polygon with animated transform
        for (int polyIdx = 0; polyIdx < PolygonCount; polyIdx++)
        {
            var points = polygonPoints[polyIdx];
            var color = colors[polyIdx % colors.Length];

            // Calculate animated transform for this polygon
            float phase = time + polyPhaseOffset[polyIdx];
            float rotation = phase * 57.2958f; // Convert radians to degrees (phase * 180/PI)
            float scale = 0.5f + 0.5f * MathF.Sin(phase); // Pulsing scale 0.5 to 1.0

            // Save context before applying per-polygon transforms
            layer.Save();

            // Set stroke color
            layer.SetStroke(color);

            // Apply transforms: translate to grid position, rotate, scale
            layer.Translate(centerX[polyIdx], centerY[polyIdx]);
            layer.Rotate(rotation);
            layer.Scale(scale, scale);

            // Draw the polygon (uses current context state)
            layer.DrawPolygon(points);

            // Restore context to default for next polygon
            layer.Restore();
        }

        // Wait for next tick (precise 60 FPS)
        if (!await timer.WaitForNextTickAsync(ct))
            break;

        // Flush frame to WebSocket
        await canvas.FlushAsync(ct);
    }
}
