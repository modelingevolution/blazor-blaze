using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BlazorBlaze.Server;

/// <summary>
/// Extension methods for MJPEG streaming endpoints.
/// </summary>
public static class MjpegEndpointExtensions
{
    private static readonly byte[] MultipartBoundary = Encoding.ASCII.GetBytes("--frame\r\n");
    private static readonly byte[] ContentTypeHeader = Encoding.ASCII.GetBytes("Content-Type: image/jpeg\r\n");
    private static readonly byte[] ContentLengthPrefix = Encoding.ASCII.GetBytes("Content-Length: ");
    private static readonly byte[] HeaderEnd = Encoding.ASCII.GetBytes("\r\n\r\n");
    private static readonly byte[] FrameEnd = Encoding.ASCII.GetBytes("\r\n");

    /// <summary>
    /// Maps an MJPEG streaming endpoint that loops the recording.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">URL pattern (e.g., "/mjpeg/{filename}").</param>
    /// <param name="basePath">Base path for MJPEG files (e.g., "wwwroot/videos").</param>
    public static IEndpointRouteBuilder MapMjpegEndpoint(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string basePath)
    {
        endpoints.MapGet(pattern, async (string filename, HttpContext context) =>
        {
            var mjpegPath = Path.Combine(basePath, filename);
            var jsonPath = mjpegPath + ".json";

            if (!File.Exists(mjpegPath) || !File.Exists(jsonPath))
            {
                context.Response.StatusCode = 404;
                return;
            }

            // Load index
            var jsonContent = await File.ReadAllTextAsync(jsonPath, context.RequestAborted);
            var metadata = JsonSerializer.Deserialize<RecordingMetadata>(jsonContent);

            if (metadata == null || metadata.Index.Count == 0)
            {
                context.Response.StatusCode = 500;
                return;
            }

            // Calculate frame interval from recording
            var frameInterval = CalculateFrameInterval(metadata);

            // Set MJPEG multipart headers
            context.Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
            context.Response.Headers["Cache-Control"] = "no-cache";

            var ct = context.RequestAborted;

            await using var mjpegStream = File.OpenRead(mjpegPath);
            using var timer = new PeriodicTimer(frameInterval);

            var frameKeys = metadata.Index.Keys.ToArray();
            int frameIndex = 0;

            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            {
                var frameSequence = frameKeys[frameIndex];
                var frame = metadata.Index[frameSequence];

                // Read frame data
                var frameData = new byte[frame.Size];
                mjpegStream.Position = (long)frame.Start;
                var bytesRead = await mjpegStream.ReadAsync(frameData.AsMemory(), ct);

                if (bytesRead != (int)frame.Size)
                    continue;

                // Write multipart frame
                await WriteMultipartFrameAsync(context.Response.Body, frameData, ct);

                // Loop
                frameIndex = (frameIndex + 1) % frameKeys.Length;
            }
        });

        return endpoints;
    }

    /// <summary>
    /// Maps a single frame endpoint for paused state.
    /// </summary>
    public static IEndpointRouteBuilder MapMjpegFrameEndpoint(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string basePath)
    {
        endpoints.MapGet(pattern, async (string filename, int frame, HttpContext context) =>
        {
            var mjpegPath = Path.Combine(basePath, filename);
            var jsonPath = mjpegPath + ".json";

            if (!File.Exists(mjpegPath) || !File.Exists(jsonPath))
            {
                context.Response.StatusCode = 404;
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath, context.RequestAborted);
            var metadata = JsonSerializer.Deserialize<RecordingMetadata>(jsonContent);

            if (metadata == null || metadata.Index.Count == 0)
            {
                context.Response.StatusCode = 500;
                return;
            }

            var frameKeys = metadata.Index.Keys.ToArray();
            var clampedFrame = Math.Clamp(frame, 0, frameKeys.Length - 1);
            var frameSequence = frameKeys[clampedFrame];
            var frameInfo = metadata.Index[frameSequence];

            await using var mjpegStream = File.OpenRead(mjpegPath);
            mjpegStream.Position = (long)frameInfo.Start;

            var frameData = new byte[frameInfo.Size];
            await mjpegStream.ReadAsync(frameData, context.RequestAborted);

            context.Response.ContentType = "image/jpeg";
            await context.Response.Body.WriteAsync(frameData, context.RequestAborted);
        });

        return endpoints;
    }

    private static TimeSpan CalculateFrameInterval(RecordingMetadata metadata)
    {
        if (metadata.Index.Count < 2)
            return TimeSpan.FromMilliseconds(33.33); // Default 30fps

        int sampleCount = Math.Min(10, metadata.Index.Count - 1);
        long totalMicroseconds = 0;

        var frames = metadata.Index.Values.ToArray();
        for (int i = 0; i < sampleCount; i++)
        {
            totalMicroseconds += (long)(frames[i + 1].RelativeTimestampMicroseconds - frames[i].RelativeTimestampMicroseconds);
        }

        double avgMicroseconds = totalMicroseconds / (double)sampleCount;
        return TimeSpan.FromMicroseconds(Math.Max(1000, avgMicroseconds)); // Minimum 1ms
    }

    private static async Task WriteMultipartFrameAsync(Stream output, byte[] jpegData, CancellationToken ct)
    {
        await output.WriteAsync(MultipartBoundary, ct);
        await output.WriteAsync(ContentTypeHeader, ct);
        await output.WriteAsync(ContentLengthPrefix, ct);

        var lengthBytes = Encoding.ASCII.GetBytes(jpegData.Length.ToString());
        await output.WriteAsync(lengthBytes, ct);

        await output.WriteAsync(HeaderEnd, ct);
        await output.WriteAsync(jpegData, ct);
        await output.WriteAsync(FrameEnd, ct);
        await output.FlushAsync(ct);
    }
}
