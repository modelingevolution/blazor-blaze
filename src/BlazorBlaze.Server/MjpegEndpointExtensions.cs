using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BlazorBlaze.Server;

/// <summary>
/// Extension methods for MJPEG streaming endpoints.
/// Includes 128MB frame caching for small recordings.
/// </summary>
public static class MjpegEndpointExtensions
{
    private const long MaxCacheSize = 128 * 1024 * 1024; // 128MB

    private static readonly byte[] MultipartBoundary = Encoding.ASCII.GetBytes("--frame\r\n");
    private static readonly byte[] ContentTypeHeader = Encoding.ASCII.GetBytes("Content-Type: image/jpeg\r\n");
    private static readonly byte[] ContentLengthPrefix = Encoding.ASCII.GetBytes("Content-Length: ");
    private static readonly byte[] HeaderEnd = Encoding.ASCII.GetBytes("\r\n\r\n");
    private static readonly byte[] FrameEnd = Encoding.ASCII.GetBytes("\r\n");

    /// <summary>
    /// Maps an MJPEG streaming endpoint that loops the recording.
    /// Files under 128MB are cached in memory for better performance.
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

            var ct = context.RequestAborted;

            // Load index
            var jsonContent = await File.ReadAllTextAsync(jsonPath, ct);
            var metadata = JsonSerializer.Deserialize<RecordingMetadata>(jsonContent);

            if (metadata == null || metadata.Index.Count == 0)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Failed to parse index or empty. Content length: {jsonContent.Length}");
                return;
            }

            var frameKeys = metadata.Index.Keys.ToArray();
            var frameCount = frameKeys.Length;

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

            // Calculate frame interval from recording
            var frameInterval = CalculateFrameInterval(metadata);

            // Set MJPEG multipart headers
            context.Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
            context.Response.Headers["Cache-Control"] = "no-cache";

            await using var mjpegStream = useCache ? null : File.OpenRead(mjpegPath);
            using var timer = new PeriodicTimer(frameInterval);

            int frameIndex = 0;

            // Send first frame immediately (don't wait for timer)
            while (!ct.IsCancellationRequested)
            {
                byte[] frameData;

                if (useCache)
                {
                    frameData = frameCache![frameIndex];
                }
                else
                {
                    var frameSequence = frameKeys[frameIndex];
                    var frame = metadata.Index[frameSequence];
                    frameData = new byte[frame.Size];
                    mjpegStream!.Position = (long)frame.Start;
                    await mjpegStream.ReadExactlyAsync(frameData, ct);
                }

                await WriteMultipartFrameAsync(context.Response.Body, frameData, ct);

                frameIndex = (frameIndex + 1) % frameCount;

                if (!await timer.WaitForNextTickAsync(ct))
                    break;
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
