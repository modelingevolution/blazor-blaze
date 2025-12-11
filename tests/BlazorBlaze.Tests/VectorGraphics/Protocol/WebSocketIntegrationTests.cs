using System.Drawing;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BlazorBlaze.VectorGraphics;
using BlazorBlaze.VectorGraphics.Protocol;
using NSubstitute;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.Tests.VectorGraphics.Protocol;

public class WebSocketIntegrationTests : IAsyncLifetime
{
    private IHost? _host;
    private TestServer? _server;
    private readonly List<byte[]> _framesForServer = new();

    public async Task InitializeAsync()
    {
        _host = await CreateTestHost();
    }

    public async Task DisposeAsync()
    {
        _host?.Dispose();
    }

    private async Task<IHost> CreateTestHost()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddSingleton(_framesForServer);
                });
                webHost.Configure(app =>
                {
                    app.UseWebSockets();
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Path == "/ws/vectorgraphics")
                        {
                            if (context.WebSockets.IsWebSocketRequest)
                            {
                                var frames = context.RequestServices.GetRequiredService<List<byte[]>>();
                                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                                // Send all queued frames
                                foreach (var frame in frames)
                                {
                                    await webSocket.SendAsync(
                                        new ArraySegment<byte>(frame),
                                        WebSocketMessageType.Binary,
                                        true,
                                        CancellationToken.None);
                                }

                                // Close the connection gracefully
                                await webSocket.CloseAsync(
                                    WebSocketCloseStatus.NormalClosure,
                                    "Frames sent",
                                    CancellationToken.None);
                            }
                            else
                            {
                                context.Response.StatusCode = 400;
                            }
                        }
                        else
                        {
                            await next();
                        }
                    });
                });
            });

        var host = await builder.StartAsync();
        _server = host.GetTestServer();
        return host;
    }

    [Fact]
    public async Task CanConnectToWebSocket()
    {
        var client = _server!.CreateWebSocketClient();

        var webSocket = await client.ConnectAsync(
            new Uri(_server.BaseAddress, "/ws/vectorgraphics"),
            CancellationToken.None);

        webSocket.State.Should().Be(WebSocketState.Open);

        await webSocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Test complete",
            CancellationToken.None);
    }

    [Fact]
    public async Task CanReceiveAndDecodeEmptyFrame()
    {
        // Create a minimal frame with no objects
        var frame = CreateMinimalFrame(frameId: 42, layerId: 0);
        _framesForServer.Add(frame);

        var client = _server!.CreateWebSocketClient();
        var webSocket = await client.ConnectAsync(
            new Uri(_server.BaseAddress, "/ws/vectorgraphics"),
            CancellationToken.None);

        // Receive the frame
        var buffer = new byte[1024];
        var result = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer),
            CancellationToken.None);

        result.MessageType.Should().Be(WebSocketMessageType.Binary);
        result.Count.Should().Be(frame.Length);

        // Decode the frame
        var decoder = new VectorGraphicsDecoder(VectorGraphicsOptions.Default);
        var mockCanvas = Substitute.For<ICanvas>();

        var decodeResult = decoder.Decode(buffer.AsSpan(0, result.Count), mockCanvas);

        decodeResult.Success.Should().BeTrue();
        decodeResult.FrameNumber.Should().Be(42UL);
        mockCanvas.Received(1).Begin(42, 0);
        mockCanvas.Received(1).End(0);
    }

    [Fact]
    public async Task CanReceiveAndDecodeFrameWithPolygon()
    {
        // Create a frame with a polygon
        var points = new[] { (100, 100), (200, 100), (200, 200), (100, 200) };
        var frame = CreateFrameWithPolygon(frameId: 1, layerId: 0, points);
        _framesForServer.Add(frame);

        var client = _server!.CreateWebSocketClient();
        var webSocket = await client.ConnectAsync(
            new Uri(_server.BaseAddress, "/ws/vectorgraphics"),
            CancellationToken.None);

        // Receive the frame
        var buffer = new byte[1024];
        var result = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer),
            CancellationToken.None);

        // Decode the frame - use TestCanvas since ReadOnlySpan<SKPoint> can't be mocked
        var decoder = new VectorGraphicsDecoder(VectorGraphicsOptions.Default);
        var testCanvas = new WebSocketTestCanvas();

        var decodeResult = decoder.Decode(buffer.AsSpan(0, result.Count), testCanvas);

        decodeResult.Success.Should().BeTrue();
        decodeResult.FrameNumber.Should().Be(1UL);

        testCanvas.CapturedPoints.Should().NotBeNull();
        testCanvas.CapturedPoints.Should().HaveCount(4);
        testCanvas.CapturedPoints![0].X.Should().Be(100);
        testCanvas.CapturedPoints![0].Y.Should().Be(100);
        testCanvas.CapturedPoints![1].X.Should().Be(200);
        testCanvas.CapturedPoints![1].Y.Should().Be(100);
    }

    [Fact]
    public async Task CanReceiveMultipleFrames()
    {
        // Add multiple frames
        _framesForServer.Add(CreateMinimalFrame(frameId: 1, layerId: 0));
        _framesForServer.Add(CreateMinimalFrame(frameId: 2, layerId: 0));
        _framesForServer.Add(CreateMinimalFrame(frameId: 3, layerId: 0));

        var client = _server!.CreateWebSocketClient();
        var webSocket = await client.ConnectAsync(
            new Uri(_server.BaseAddress, "/ws/vectorgraphics"),
            CancellationToken.None);

        var decoder = new VectorGraphicsDecoder(VectorGraphicsOptions.Default);
        var mockCanvas = Substitute.For<ICanvas>();
        var receivedFrameIds = new List<ulong>();

        var buffer = new byte[1024];

        // Receive all frames
        for (int i = 0; i < 3; i++)
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var decodeResult = decoder.Decode(buffer.AsSpan(0, result.Count), mockCanvas);
            if (decodeResult.Success && decodeResult.FrameNumber.HasValue)
            {
                receivedFrameIds.Add(decodeResult.FrameNumber.Value);
            }
        }

        receivedFrameIds.Should().BeEquivalentTo(new ulong[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CanReceiveAndDecodeFrameWithText()
    {
        var frame = CreateFrameWithText(frameId: 5, layerId: 1, "Hello WebSocket", x: 50, y: 100, fontSize: 14);
        _framesForServer.Add(frame);

        var client = _server!.CreateWebSocketClient();
        var webSocket = await client.ConnectAsync(
            new Uri(_server.BaseAddress, "/ws/vectorgraphics"),
            CancellationToken.None);

        var buffer = new byte[1024];
        var result = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer),
            CancellationToken.None);

        var decoder = new VectorGraphicsDecoder(VectorGraphicsOptions.Default);
        var testCanvas = new WebSocketTestCanvas();

        var decodeResult = decoder.Decode(buffer.AsSpan(0, result.Count), testCanvas);

        decodeResult.Success.Should().BeTrue();
        decodeResult.FrameNumber.Should().Be(5UL);
        testCanvas.LastText.Should().Be("Hello WebSocket");
        testCanvas.LastTextX.Should().Be(50);
        testCanvas.LastTextY.Should().Be(100);
    }

    [Fact]
    public async Task DecoderHandlesFragmentedData()
    {
        // Test that decoder can handle partial data
        var frame = CreateMinimalFrame(frameId: 99, layerId: 2);

        var decoder = new VectorGraphicsDecoder(VectorGraphicsOptions.Default);
        var mockCanvas = Substitute.For<ICanvas>();

        // First, try decoding only partial data (should need more)
        var partialResult = decoder.Decode(frame.AsSpan(0, 5), mockCanvas);
        partialResult.Success.Should().BeFalse();
        partialResult.BytesConsumed.Should().Be(0);

        // Then decode the full frame
        var fullResult = decoder.Decode(frame, mockCanvas);
        fullResult.Success.Should().BeTrue();
        fullResult.FrameNumber.Should().Be(99UL);
    }

    #region Helper Methods

    private static byte[] CreateMinimalFrame(ulong frameId, byte layerId)
    {
        var buffer = new byte[13];
        int offset = 0;

        buffer[offset++] = 0x00; // Frame type
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), frameId);
        offset += 8;
        buffer[offset++] = layerId;
        buffer[offset++] = 0x00; // Object count
        buffer[offset++] = 0xFF; // End marker
        buffer[offset++] = 0xFF;

        return buffer;
    }

    private static byte[] CreateFrameWithPolygon(ulong frameId, byte layerId, (int X, int Y)[] points)
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Frame header
        buffer[offset++] = 0x00; // Frame type (master)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), frameId);
        offset += 8;
        buffer[offset++] = layerId;

        // Object count: 1
        buffer[offset++] = 0x01;

        // Object type: Polygon (1)
        buffer[offset++] = 0x01;

        // Point count (varint)
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), (uint)points.Length);

        // First point (absolute, zigzag encoded)
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[0].X);
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[0].Y);

        // Remaining points (delta encoded)
        int lastX = points[0].X;
        int lastY = points[0].Y;
        for (int i = 1; i < points.Length; i++)
        {
            offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[i].X - lastX);
            offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[i].Y - lastY);
            lastX = points[i].X;
            lastY = points[i].Y;
        }

        // Context (no stroke/fill)
        buffer[offset++] = 0x00;

        // End marker
        buffer[offset++] = 0xFF;
        buffer[offset++] = 0xFF;

        return buffer.AsSpan(0, offset).ToArray();
    }

    private static byte[] CreateFrameWithText(ulong frameId, byte layerId, string text, int x, int y, ushort fontSize)
    {
        var buffer = new byte[1024];
        int offset = 0;

        // Frame header
        buffer[offset++] = 0x00; // Frame type (master)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), frameId);
        offset += 8;
        buffer[offset++] = layerId;

        // Object count: 1
        buffer[offset++] = 0x01;

        // Object type: Text (2)
        buffer[offset++] = 0x02;

        // X, Y (zigzag encoded)
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), x);
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), y);

        // Text length and UTF8 bytes
        var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), (uint)textBytes.Length);
        textBytes.CopyTo(buffer.AsSpan(offset));
        offset += textBytes.Length;

        // Context with font size
        byte contextFlags = 0x08; // HasFontSize
        buffer[offset++] = contextFlags;
        offset += BinaryEncoding.WriteVarint(buffer.AsSpan(offset), fontSize);

        // End marker
        buffer[offset++] = 0xFF;
        buffer[offset++] = 0xFF;

        return buffer.AsSpan(0, offset).ToArray();
    }

    #endregion
}

/// <summary>
/// Test implementation of ICanvas that captures all draw calls for verification.
/// Used because ReadOnlySpan&lt;SKPoint&gt; cannot be mocked with NSubstitute.
/// </summary>
internal class WebSocketTestCanvas : ICanvas
{
    public byte LayerId { get; set; }
    public object Sync { get; } = new object();

    public int BeginCalls { get; private set; }
    public int EndCalls { get; private set; }
    public int DrawPolygonCalls { get; private set; }
    public int DrawTextCalls { get; private set; }

    public SKPoint[]? CapturedPoints { get; private set; }
    public RgbColor? LastPolygonColor { get; private set; }
    public int LastPolygonWidth { get; private set; }

    public string? LastText { get; private set; }
    public int LastTextX { get; private set; }
    public int LastTextY { get; private set; }
    public int LastFontSize { get; private set; }

    public void Begin(ulong frameNr, byte? layerId = null) => BeginCalls++;

    public void End(byte? layerId = null) => EndCalls++;

    public void DrawPolygon(ReadOnlySpan<SKPoint> points, RgbColor? color = null, int width = 1, byte? layerId = null)
    {
        DrawPolygonCalls++;
        CapturedPoints = points.ToArray();
        LastPolygonColor = color;
        LastPolygonWidth = width;
    }

    public void DrawRectangle(Rectangle rect, RgbColor? color = null, byte? layerId = null) { }

    public void DrawText(string text, int x = 0, int y = 0, int size = 12, RgbColor? color = null, byte? layerId = null)
    {
        DrawTextCalls++;
        LastText = text;
        LastTextX = x;
        LastTextY = y;
        LastFontSize = size;
    }
}
