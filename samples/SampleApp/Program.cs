using System.Text.Json;
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

// WebSocket endpoint for 20K point stress test (multi-layer, stateful context)
app.MapVectorGraphicsEndpoint("/ws/stress20k", StreamStressTestFramesV2);

// WebSocket endpoint for protocol test - tests all draw types and layer composition
app.MapVectorGraphicsEndpoint("/ws/protocol-test", PatternType.MultiLayer);

// Simple bouncing ball test - minimal protocol usage for debugging
app.MapVectorGraphicsEndpoint("/ws/test-ball", PatternType.BouncingBall);

// Calibration pattern for visual verification
app.MapVectorGraphicsEndpoint("/ws/calibration", PatternType.Calibration);

// MJPEG + Ball: JPEG frames on layer 0, bouncing ball on layer 1
var mjpegPath = Path.Combine(app.Environment.WebRootPath, "videos", "test-ball-mjpeg");
app.MapVectorGraphicsEndpoint("/ws/mjpeg-ball", async (IRemoteCanvasV2 canvas, CancellationToken ct) =>
{
    await StreamMjpegWithBallAsync(canvas, mjpegPath, ct);
});

// MJPEG streaming endpoint for looping playback
var videosPath = Path.Combine(app.Environment.WebRootPath, "videos");
app.MapMjpegEndpoint("/mjpeg/{filename}", videosPath);
app.MapMjpegFrameEndpoint("/mjpeg-frame/{filename}/{frame:int}", videosPath);

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

static async Task StreamMjpegWithBallAsync(IRemoteCanvasV2 canvas, string mjpegPath, CancellationToken ct)
{
    const int width = 1920, height = 1080;
    const int ballRadius = 40;
    const long MaxCacheSize = 128 * 1024 * 1024; // 128MB

    // Load MJPEG index
    var jsonPath = mjpegPath + ".json";
    if (!File.Exists(mjpegPath) || !File.Exists(jsonPath))
        throw new FileNotFoundException($"MJPEG files not found: {mjpegPath}");

    var jsonContent = await File.ReadAllTextAsync(jsonPath, ct);
    var metadata = JsonSerializer.Deserialize<RecordingMetadata>(jsonContent)
        ?? throw new InvalidOperationException("Failed to parse MJPEG index");

    var frameKeys = metadata.Index.Keys.ToArray();
    var frameCount = frameKeys.Length;

    if (frameCount == 0)
        throw new InvalidOperationException("MJPEG index has no frames");

    // Check file size and cache if under 128MB
    var fileInfo = new FileInfo(mjpegPath);
    var useCache = fileInfo.Length <= MaxCacheSize;
    byte[][]? frameCache = null;

    if (useCache)
    {
        // Pre-load all frames into memory
        frameCache = new byte[frameCount][];
        await using var cacheStream = File.OpenRead(mjpegPath);

        for (int i = 0; i < frameCount; i++)
        {
            var frameSequence = frameKeys[i];
            var frame = metadata.Index[frameSequence];
            frameCache[i] = new byte[frame.Size];
            cacheStream.Position = (long)frame.Start;
            await cacheStream.ReadExactlyAsync(frameCache[i], ct);
        }
    }

    // Ball animation state
    float ballX = width / 2f, ballY = height / 2f;
    float dx = 8, dy = 6;

    await using var mjpegStream = useCache ? null : File.OpenRead(mjpegPath);
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(33)); // ~30fps

    int frameIndex = 0;

    while (!ct.IsCancellationRequested)
    {
        canvas.BeginFrame();

        // Layer 0: MJPEG frame (Master every frame)
        var layer0 = canvas.Layer(0);
        layer0.Master();

        byte[] jpegData;
        if (useCache)
        {
            // Read from cache
            jpegData = frameCache![frameIndex];
        }
        else
        {
            // Read from disk
            var frameSequence = frameKeys[frameIndex];
            var frame = metadata.Index[frameSequence];
            jpegData = new byte[frame.Size];
            mjpegStream!.Position = (long)frame.Start;
            await mjpegStream.ReadExactlyAsync(jpegData, ct);
        }

        // Draw JPEG as background
        layer0.DrawJpeg(jpegData, 0, 0, width, height);

        // Layer 1: Bouncing ball overlay
        var layer1 = canvas.Layer(1);
        layer1.Master();

        // Update ball position
        ballX += dx;
        ballY += dy;
        if (ballX - ballRadius <= 0 || ballX + ballRadius >= width) dx = -dx;
        if (ballY - ballRadius <= 0 || ballY + ballRadius >= height) dy = -dy;

        // Draw red ball
        layer1.SetFill(new RgbColor(255, 50, 50));
        layer1.SetStroke(RgbColor.White);
        layer1.SetThickness(3);
        layer1.DrawCircle((int)ballX, (int)ballY, ballRadius);

        // Draw frame info text
        layer1.SetFontSize(20);
        layer1.SetFontColor(RgbColor.White);
        var cacheStatus = useCache ? "CACHED" : "DISK";
        layer1.DrawText($"Frame: {canvas.FrameId} | MJPEG: {frameIndex}/{frameCount} ({cacheStatus})", 20, 40);

        await canvas.FlushAsync(ct);

        // Loop MJPEG
        frameIndex = (frameIndex + 1) % frameCount;

        if (!await timer.WaitForNextTickAsync(ct))
            break;
    }
}

