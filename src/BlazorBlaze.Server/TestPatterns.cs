using BlazorBlaze.VectorGraphics;
using SkiaSharp;

namespace BlazorBlaze.Server;

/// <summary>
/// Built-in test patterns for VectorGraphics protocol testing and calibration.
/// </summary>
public enum PatternType
{
    /// <summary>
    /// Simple bouncing ball animation. Single layer, Master mode only.
    /// Tests: Circle, Text, basic protocol round-trip.
    /// </summary>
    BouncingBall,

    /// <summary>
    /// Multi-layer composition test with Master/Remain switching.
    /// Layer 0: Static background (Remain), Layer 1: Animated (Master), Layer 2: Periodic updates.
    /// Tests: All draw types, layer composition, Remain/Master/Clear modes.
    /// </summary>
    MultiLayer,

    /// <summary>
    /// Calibration pattern with alignment markers and measurement grid.
    /// Static pattern for visual verification of rendering accuracy.
    /// Tests: Lines, rectangles, text positioning, coordinate system.
    /// </summary>
    Calibration
}

/// <summary>
/// Implementations of built-in test patterns.
/// </summary>
public static class TestPatterns
{
    public static async Task BouncingBallAsync(IRemoteCanvasV2 canvas, CancellationToken ct)
    {
        float x = 600, y = 400;
        float dx = 4, dy = 3;
        const int radius = 30;
        const int width = 1200, height = 800;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));

        while (!ct.IsCancellationRequested)
        {
            canvas.BeginFrame();

            var layer = canvas.Layer(0);
            layer.Master();

            layer.SetFill(new RgbColor(255, 50, 50));
            layer.SetStroke(RgbColor.White);
            layer.SetThickness(3);
            layer.DrawCircle((int)x, (int)y, radius);

            layer.SetFontSize(16);
            layer.SetFontColor(RgbColor.White);
            layer.DrawText($"Frame: {canvas.FrameId}", 20, 30);
            layer.DrawText($"Position: ({(int)x}, {(int)y})", 20, 55);

            x += dx;
            y += dy;

            if (x - radius <= 0 || x + radius >= width) dx = -dx;
            if (y - radius <= 0 || y + radius >= height) dy = -dy;

            await canvas.FlushAsync(ct);

            if (!await timer.WaitForNextTickAsync(ct))
                break;
        }
    }

    public static async Task MultiLayerAsync(IRemoteCanvasV2 canvas, CancellationToken ct)
    {
        var pentagonPoints = new SKPoint[5];
        for (int i = 0; i < 5; i++)
        {
            float angle = MathF.PI * 2f * i / 5f - MathF.PI / 2f;
            pentagonPoints[i] = new SKPoint(50 * MathF.Cos(angle), 50 * MathF.Sin(angle));
        }

        var trianglePoints = new SKPoint[]
        {
            new(0, -40),
            new(35, 30),
            new(-35, 30)
        };

        float time = 0f;
        int frameCount = 0;
        bool firstFrame = true;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));

        while (!ct.IsCancellationRequested)
        {
            time += 1f / 60f;
            frameCount++;

            canvas.BeginFrame();

            // Layer 0: Static background (Remain after first frame)
            var layer0 = canvas.Layer(0);
            if (firstFrame)
            {
                layer0.Master();

                layer0.SetStroke(new RgbColor(50, 50, 50));
                layer0.SetThickness(1);
                for (int x = 0; x <= 1200; x += 100)
                    layer0.DrawLine(x, 0, x, 800);
                for (int y = 0; y <= 800; y += 100)
                    layer0.DrawLine(0, y, 1200, y);

                layer0.SetFontSize(16);
                layer0.SetFontColor(new RgbColor(150, 150, 150));
                layer0.DrawText("CIRCLE", 80, 130);
                layer0.DrawText("RECT", 280, 130);
                layer0.DrawText("LINE", 480, 130);
                layer0.DrawText("POLYGON", 680, 130);
                layer0.DrawText("TEXT", 880, 130);
                layer0.DrawText("LAYER2 (Remain/Master)", 80, 530);
            }
            else
            {
                layer0.Remain();
            }

            // Layer 1: Animated draw types (Master every frame)
            var layer1 = canvas.Layer(1);
            layer1.Master();

            // Circle
            layer1.Save();
            layer1.Translate(100, 250);
            float circleRadius = 30 + 20 * MathF.Sin(time * 2);
            layer1.SetStroke(new RgbColor(255, 100, 100));
            layer1.SetFill(new RgbColor(255, 100, 100, 100));
            layer1.SetThickness(3);
            layer1.DrawCircle(0, 0, (int)circleRadius);
            layer1.Restore();

            // Rectangle
            layer1.Save();
            layer1.Translate(300, 250);
            layer1.Rotate(time * 45);
            layer1.SetStroke(new RgbColor(100, 255, 100));
            layer1.SetThickness(2);
            layer1.DrawRectangle(-40, -30, 80, 60);
            layer1.Restore();

            // Line
            layer1.Save();
            layer1.Translate(500, 250);
            layer1.Rotate(time * 90);
            layer1.SetStroke(new RgbColor(100, 100, 255));
            layer1.SetThickness(4);
            layer1.DrawLine(-60, 0, 60, 0);
            layer1.SetStroke(new RgbColor(255, 255, 100));
            layer1.DrawLine(0, -60, 0, 60);
            layer1.Restore();

            // Polygon
            layer1.Save();
            layer1.Translate(700, 250);
            layer1.Rotate(time * 30);
            float scale = 0.8f + 0.4f * MathF.Sin(time * 3);
            layer1.Scale(scale, scale);
            layer1.SetStroke(new RgbColor(255, 100, 255));
            layer1.SetThickness(2);
            layer1.DrawPolygon(pentagonPoints);
            layer1.Restore();

            // Text
            layer1.Save();
            layer1.Translate(900, 220);
            layer1.SetFontSize(24);
            layer1.SetFontColor(new RgbColor(255, 200, 100));
            layer1.DrawText($"Frame: {canvas.FrameId}", 0, 0);
            layer1.SetFontSize(18);
            layer1.SetFontColor(new RgbColor(200, 200, 255));
            layer1.DrawText($"Time: {time:F2}s", 0, 30);
            layer1.Restore();

            // Moving triangle
            layer1.Save();
            float triangleX = (time * 100) % 1400 - 100;
            layer1.Translate(triangleX, 400);
            layer1.SetStroke(new RgbColor(0, 255, 255));
            layer1.SetThickness(2);
            layer1.DrawPolygon(trianglePoints);
            layer1.Restore();

            // Layer 2: Periodic updates (tests Remain/Master switching)
            var layer2 = canvas.Layer(2);
            if (frameCount % 60 == 1)
            {
                layer2.Master();

                int colorIndex = (frameCount / 60) % 6;
                var colors = new RgbColor[]
                {
                    new(255, 0, 0), new(0, 255, 0), new(0, 0, 255),
                    new(255, 255, 0), new(255, 0, 255), new(0, 255, 255)
                };

                layer2.SetStroke(colors[colorIndex]);
                layer2.SetThickness(4);
                layer2.DrawRectangle(80, 550, 200, 100);

                layer2.SetFontSize(20);
                layer2.SetFontColor(colors[colorIndex]);
                layer2.DrawText($"Update #{frameCount / 60 + 1}", 100, 620);
            }
            else
            {
                layer2.Remain();
            }

            firstFrame = false;

            if (!await timer.WaitForNextTickAsync(ct))
                break;

            await canvas.FlushAsync(ct);
        }
    }

    public static async Task CalibrationAsync(IRemoteCanvasV2 canvas, CancellationToken ct)
    {
        const int width = 1200, height = 800;
        const int gridSize = 100;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500)); // 2 FPS for calibration

        bool firstFrame = true;

        while (!ct.IsCancellationRequested)
        {
            canvas.BeginFrame();

            var layer0 = canvas.Layer(0);

            if (firstFrame)
            {
                layer0.Master();

                // Draw grid
                layer0.SetStroke(new RgbColor(40, 40, 40));
                layer0.SetThickness(1);
                for (int x = 0; x <= width; x += gridSize)
                    layer0.DrawLine(x, 0, x, height);
                for (int y = 0; y <= height; y += gridSize)
                    layer0.DrawLine(0, y, width, y);

                // Draw major axes
                layer0.SetStroke(new RgbColor(100, 100, 100));
                layer0.SetThickness(2);
                layer0.DrawLine(width / 2, 0, width / 2, height); // Vertical center
                layer0.DrawLine(0, height / 2, width, height / 2); // Horizontal center

                // Draw corner markers
                layer0.SetStroke(RgbColor.Red);
                layer0.SetThickness(3);
                // Top-left
                layer0.DrawLine(0, 0, 50, 0);
                layer0.DrawLine(0, 0, 0, 50);
                // Top-right
                layer0.DrawLine(width, 0, width - 50, 0);
                layer0.DrawLine(width, 0, width, 50);
                // Bottom-left
                layer0.DrawLine(0, height, 50, height);
                layer0.DrawLine(0, height, 0, height - 50);
                // Bottom-right
                layer0.DrawLine(width, height, width - 50, height);
                layer0.DrawLine(width, height, width, height - 50);

                // Draw center crosshair
                layer0.SetStroke(new RgbColor(0, 255, 0));
                layer0.SetThickness(2);
                int cx = width / 2, cy = height / 2;
                layer0.DrawLine(cx - 30, cy, cx + 30, cy);
                layer0.DrawLine(cx, cy - 30, cx, cy + 30);
                layer0.DrawCircle(cx, cy, 20);

                // Draw coordinate labels
                layer0.SetFontSize(12);
                layer0.SetFontColor(new RgbColor(150, 150, 150));
                for (int x = 0; x <= width; x += gridSize)
                {
                    layer0.DrawText($"{x}", x + 2, 12);
                }
                for (int y = gridSize; y <= height; y += gridSize)
                {
                    layer0.DrawText($"{y}", 2, y - 2);
                }

                // Draw title
                layer0.SetFontSize(24);
                layer0.SetFontColor(RgbColor.White);
                layer0.DrawText("CALIBRATION PATTERN", width / 2 - 120, 40);

                // Draw resolution info
                layer0.SetFontSize(16);
                layer0.SetFontColor(new RgbColor(200, 200, 200));
                layer0.DrawText($"Resolution: {width}x{height}", width / 2 - 70, 70);
                layer0.DrawText($"Grid: {gridSize}px", width / 2 - 35, 90);

                // Draw test shapes at known positions
                layer0.SetStroke(new RgbColor(255, 255, 0));
                layer0.SetThickness(2);
                // Rectangle at (100, 100) with size 100x100
                layer0.DrawRectangle(100, 100, 100, 100);
                layer0.SetFontSize(10);
                layer0.SetFontColor(new RgbColor(255, 255, 0));
                layer0.DrawText("100x100 @ (100,100)", 105, 215);

                // Circle at (900, 150) with radius 50
                layer0.SetStroke(new RgbColor(0, 255, 255));
                layer0.DrawCircle(900, 150, 50);
                layer0.SetFontColor(new RgbColor(0, 255, 255));
                layer0.DrawText("r=50 @ (900,150)", 855, 215);

                firstFrame = false;
            }
            else
            {
                layer0.Remain();
            }

            // Layer 1: Frame counter (proves connection is live)
            var layer1 = canvas.Layer(1);
            layer1.Master();
            layer1.SetFontSize(14);
            layer1.SetFontColor(new RgbColor(100, 255, 100));
            layer1.DrawText($"Frame: {canvas.FrameId}", width - 100, height - 20);

            if (!await timer.WaitForNextTickAsync(ct))
                break;

            await canvas.FlushAsync(ct);
        }
    }
}
