# IRenderingStream Design

## Overview

`IRenderingStream` is the core abstraction for consuming streaming data and rendering it to a SkiaSharp canvas. It hides protocol details behind a clean interface, enabling:

1. **VectorGraphics protocol** - Built-in, improved binary protocol for general-purpose vector streaming
2. **Consistent API** - Same usage pattern regardless of protocol
3. **Pluggable architecture** - Easy to add custom protocols (e.g., Keypoints, Segmentation in rocket-welder2)
4. **Off-the-shelf solution** - VectorGraphics ready to use out of the box

## Core Interface

```csharp
namespace ModelingEvolution.BlazorBlaze.VectorGraphics;

/// <summary>
/// A stream that produces rendering commands from an external data source.
/// Abstracts protocol details and provides a consistent rendering API.
/// </summary>
public interface IRenderingStream : IAsyncDisposable
{
    /// <summary>
    /// Connect to the data source.
    /// </summary>
    Task ConnectAsync(Uri uri, CancellationToken ct = default);

    /// <summary>
    /// Disconnect from the data source.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Whether the stream is currently connected and receiving data.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Current frame number.
    /// </summary>
    ulong Frame { get; }

    /// <summary>
    /// Frames per second (measured).
    /// </summary>
    float Fps { get; }

    /// <summary>
    /// Last error message, if any.
    /// </summary>
    string? Error { get; }

    /// <summary>
    /// Render current state to canvas.
    /// Thread-safe - can be called from render loop.
    /// </summary>
    void Render(SKCanvas canvas);
}
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         IRenderingStream                             │
│  (public API - what users interact with)                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    Transport Layer                            │   │
│  │  (WebSocket, TCP, Memory, File - abstracted)                 │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    IFrameDecoder                              │   │
│  │  (protocol-specific parsing - pluggable)                     │   │
│  │                                                               │   │
│  │  ┌─────────────────────────┐  ┌────────────────────────────┐ │   │
│  │  │ VectorGraphicsDecoder   │  │ Custom decoders            │ │   │
│  │  │ (built-in)              │  │ (e.g., in rocket-welder2:  │ │   │
│  │  │                         │  │  KeypointsDecoder,         │ │   │
│  │  │                         │  │  SegmentationDecoder)      │ │   │
│  │  └─────────────────────────┘  └────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    SkiaCanvas                                 │   │
│  │  (internal rendering buffer with layer support)              │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

## IFrameDecoder Interface

Internal interface for protocol-specific frame parsing:

```csharp
/// <summary>
/// Decodes raw stream data into canvas rendering commands.
/// Implementations are protocol-specific.
/// </summary>
public interface IFrameDecoder
{
    /// <summary>
    /// Decode a frame from the buffer and render to canvas.
    /// </summary>
    /// <param name="buffer">Input buffer (may contain partial data)</param>
    /// <param name="canvas">Target canvas for rendering</param>
    /// <returns>
    /// DecodeResult with frame number if complete, bytes consumed,
    /// or indication that more data is needed.
    /// </returns>
    DecodeResult Decode(ReadOnlySpan<byte> buffer, ICanvas canvas);

    /// <summary>
    /// Reset decoder state (e.g., clear delta tracking).
    /// </summary>
    void Reset();
}

public readonly record struct DecodeResult
{
    public bool Success { get; init; }
    public int BytesConsumed { get; init; }
    public ulong? FrameNumber { get; init; }

    public static DecodeResult NeedMoreData(int consumed = 0)
        => new() { Success = false, BytesConsumed = consumed };

    public static DecodeResult Frame(ulong frameNumber, int consumed)
        => new() { Success = true, FrameNumber = frameNumber, BytesConsumed = consumed };
}
```

## Built-in Implementation

### RenderingStream (Base Implementation)

```csharp
/// <summary>
/// Base implementation handling connection, buffering, and lifecycle.
/// </summary>
public class RenderingStream : IRenderingStream
{
    private readonly IFrameDecoder _decoder;
    private readonly SkiaCanvas _canvas;
    private readonly ILogger _logger;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;

    public RenderingStream(IFrameDecoder decoder, ILoggerFactory loggerFactory)
    {
        _decoder = decoder;
        _canvas = new SkiaCanvas();
        _logger = loggerFactory.CreateLogger<RenderingStream>();
    }

    public bool IsConnected { get; private set; }
    public ulong Frame { get; private set; }
    public float Fps { get; private set; }
    public string? Error { get; private set; }

    public async Task ConnectAsync(Uri uri, CancellationToken ct = default)
    {
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(uri, ct);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsConnected = true;
        _ = Task.Factory.StartNew(ReceiveLoop, TaskCreationOptions.LongRunning);
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_socket?.State == WebSocketState.Open)
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", default);
        IsConnected = false;
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[8 * 1024 * 1024];
        int offset = 0;
        var fpsWatch = new FpsWatch();

        try
        {
            while (_socket!.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
                var result = await _socket.ReceiveAsync(segment, _cts.Token);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var data = buffer.AsSpan(0, offset + result.Count);
                    var decoded = _decoder.Decode(data, _canvas);

                    if (decoded.Success && decoded.FrameNumber.HasValue)
                    {
                        Frame = decoded.FrameNumber.Value;
                        Fps = (float)fpsWatch++.Value;
                        offset = 0;

                        // Shift remaining data to start
                        if (decoded.BytesConsumed < data.Length)
                        {
                            var remaining = data.Length - decoded.BytesConsumed;
                            data.Slice(decoded.BytesConsumed).CopyTo(buffer);
                            offset = remaining;
                        }
                    }
                    else
                    {
                        offset += result.Count;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _logger.LogError(ex, "RenderingStream error");
        }
        finally
        {
            IsConnected = false;
        }
    }

    public void Render(SKCanvas canvas)
    {
        canvas.Clear();
        _canvas.Render(canvas);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _socket?.Dispose();
    }
}
```

### Factory Method

```csharp
public static class RenderingStreams
{
    /// <summary>
    /// Create a rendering stream for VectorGraphics protocol.
    /// This is the built-in protocol for general-purpose vector streaming.
    /// </summary>
    public static IRenderingStream VectorGraphics(
        ILoggerFactory loggerFactory,
        VectorGraphicsOptions? options = null)
    {
        options ??= VectorGraphicsOptions.Default;
        return new RenderingStream(
            new VectorGraphicsDecoder(options),
            loggerFactory);
    }
}
```

For custom protocols, users simply instantiate `RenderingStream` directly or implement `IRenderingStream`:

```csharp
// Option 1: Use RenderingStream with custom decoder
var stream = new RenderingStream(new KeypointsDecoder(options), loggerFactory);

// Option 2: Implement IRenderingStream directly
public class KeypointsRenderingStream : IRenderingStream
{
    // Full control over connection, buffering, rendering
}
```

### VectorGraphicsOptions

```csharp
public record VectorGraphicsOptions
{
    public static readonly VectorGraphicsOptions Default = new();

    /// <summary>
    /// Use binary protocol (improved) instead of protobuf (legacy).
    /// </summary>
    public bool UseBinaryProtocol { get; init; } = true;

    /// <summary>
    /// Layers to render (null = all layers).
    /// </summary>
    public int[]? FilteredLayers { get; init; }
}
```

## VectorGraphics Protocol (Improved)

The VectorGraphics protocol is improved as a proper, off-the-shelf solution for general-purpose vector streaming:

### Wire Format

```
Frame:
[FrameType: 1 byte]         // 0x00 = master, 0x01 = delta
[FrameId: 8 bytes LE]
[LayerId: 1 byte]
[ObjectCount: varint]
For each object:
    [ObjectType: 1 byte]    // 1=Polygon, 2=Text, 3=Circle, 4=Rect
    [ObjectData: type-specific binary encoding]
    [HasContext: 1 byte]    // 0 or flags
    [Context: optional DrawContext]
[EndMarker: 2 bytes = 0xFF 0xFF]
```

### Object Encodings

**Polygon:**
```
[PointCount: varint]
[FirstX: varint+zigzag]
[FirstY: varint+zigzag]
For remaining points:
    [DeltaX: varint+zigzag]
    [DeltaY: varint+zigzag]
```

**Text:**
```
[X: varint+zigzag]
[Y: varint+zigzag]
[Length: varint]
[UTF8Bytes: Length bytes]
```

**Circle:**
```
[CenterX: varint+zigzag]
[CenterY: varint+zigzag]
[Radius: varint]
```

**Rectangle:**
```
[X: varint+zigzag]
[Y: varint+zigzag]
[Width: varint]
[Height: varint]
```

**DrawContext (if present):**
```
[Flags: 1 byte]
    bit 0: HasStroke
    bit 1: HasFill
    bit 2: HasThickness
    bit 3: HasFontSize
    bit 4: HasFontColor
    bit 5: HasOffset
If HasStroke: [R][G][B][A] (4 bytes)
If HasFill: [R][G][B][A] (4 bytes)
If HasThickness: [varint]
If HasFontSize: [varint]
If HasFontColor: [R][G][B][A] (4 bytes)
If HasOffset: [X: varint+zigzag][Y: varint+zigzag]
```

## Usage Examples

### Example 1: VectorGraphics (Built-in)

```csharp
@page "/vectorgraphics"
@implements IAsyncDisposable

<SKCanvasView OnPaintSurface="OnPaint" />
<p>Frame: @(_stream?.Frame) | FPS: @(_stream?.Fps:F1)</p>

@code {
    private IRenderingStream? _stream;

    protected override async Task OnInitializedAsync()
    {
        _stream = RenderingStreams.VectorGraphics(LoggerFactory, new VectorGraphicsOptions
        {
            UseBinaryProtocol = true,
            FilteredLayers = new[] { 0, 1 } // Only layers 0 and 1
        });

        await _stream.ConnectAsync(new Uri("ws://server:8080/graphics"));
    }

    private void OnPaint(SKPaintSurfaceEventArgs e)
    {
        _stream?.Render(e.Surface.Canvas);
        StateHasChanged(); // Update FPS display
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
            await _stream.DisposeAsync();
    }
}
```

### Example 2: Custom Protocol (e.g., in rocket-welder2)

This shows how rocket-welder2 would implement Keypoints/Segmentation decoders:

```csharp
// In rocket-welder2 library:
namespace ModelingEvolution.RocketWelder.Rendering;

/// <summary>
/// Decoder for keypoints protocol from rocket-welder-sdk.
/// Implemented in rocket-welder2, not BlazorBlaze.
/// </summary>
public class KeypointsDecoder : IFrameDecoder
{
    private readonly KeypointsOptions _options;
    private readonly Dictionary<int, (int X, int Y, float Conf)> _state = new();

    public KeypointsDecoder(KeypointsOptions options)
    {
        _options = options;
    }

    public DecodeResult Decode(ReadOnlySpan<byte> buffer, ICanvas canvas)
    {
        // Read frame type, frame ID, keypoints using varint+zigzag
        // Reconstruct positions from delta encoding
        // Draw circles at keypoint positions
        // Draw skeleton connections if configured

        foreach (var (id, (x, y, conf)) in _state)
        {
            if (conf >= _options.ConfidenceThreshold)
            {
                canvas.DrawCircle(x, y, _options.KeypointRadius * conf,
                    _options.GetColor(id), layerId: _options.LayerId);
            }
        }

        return DecodeResult.Frame(frameId, bytesConsumed);
    }

    public void Reset() => _state.Clear();
}

// Usage in rocket-welder2 Blazor component:
var stream = new RenderingStream(new KeypointsDecoder(options), loggerFactory);
await stream.ConnectAsync(new Uri("ws://device:8080/keypoints"));
```

```csharp
// Similarly for segmentation in rocket-welder2:
public class SegmentationDecoder : IFrameDecoder
{
    public DecodeResult Decode(ReadOnlySpan<byte> buffer, ICanvas canvas)
    {
        // Read frame ID, dimensions
        // For each instance: read classId, instanceId, contour points (delta encoded)
        // Draw filled/outlined polygons with class colors

        canvas.DrawFilledPolygon(points, classColor.WithAlpha(128), classColor, layerId);

        return DecodeResult.Frame(frameId, bytesConsumed);
    }
}
```

## Sample App Integration

The SampleApp demonstrates the built-in VectorGraphics stream:

```csharp
@page "/stream-demo"
@implements IAsyncDisposable

<h1>VectorGraphics Stream Demo</h1>

<div class="controls">
    <input @bind="_serverUrl" placeholder="ws://localhost:8080/graphics" />
    <button @onclick="Connect" disabled="@_stream?.IsConnected">Connect</button>
    <button @onclick="Disconnect" disabled="@(_stream?.IsConnected != true)">Disconnect</button>
</div>

<div class="canvas-container">
    <SKCanvasView OnPaintSurface="OnPaint" style="width: 100%; height: 500px;" />
</div>

<div class="stats">
    <span>Frame: @(_stream?.Frame)</span>
    <span>FPS: @(_stream?.Fps:F1)</span>
    <span>Status: @(_stream?.IsConnected == true ? "Connected" : "Disconnected")</span>
    @if (_stream?.Error != null)
    {
        <span class="error">Error: @_stream.Error</span>
    }
</div>

@code {
    private IRenderingStream? _stream;
    private string _serverUrl = "ws://localhost:8080/graphics";

    protected override void OnInitialized()
    {
        _stream = RenderingStreams.VectorGraphics(LoggerFactory);
    }

    private async Task Connect()
    {
        await _stream!.ConnectAsync(new Uri(_serverUrl));
    }

    private async Task Disconnect()
    {
        await _stream!.DisconnectAsync();
    }

    private void OnPaint(SKPaintSurfaceEventArgs e)
    {
        _stream?.Render(e.Surface.Canvas);
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
            await _stream.DisposeAsync();
    }
}
```

## File Structure

### BlazorBlaze (this library)

```
src/ModelingEvolution.BlazorBlaze/VectorGraphics/
├── IRenderingStream.cs              # Core interface
├── RenderingStream.cs               # Base implementation
├── RenderingStreams.cs              # Factory methods
├── Protocol/
│   ├── IFrameDecoder.cs             # Decoder interface (public, for custom implementations)
│   ├── DecodeResult.cs              # Decode result struct
│   ├── BinaryEncoding.cs            # Varint + ZigZag utilities (public, reusable)
│   └── VectorGraphicsDecoder.cs     # Built-in VectorGraphics protocol
├── Options/
│   └── VectorGraphicsOptions.cs
├── Point32.cs                       # Internal coordinate type
├── SkiaCanvas.cs                    # Internal rendering
└── ...
```

### rocket-welder2 (custom decoders)

```
src/ModelingEvolution.RocketWelder.Client/Rendering/
├── KeypointsDecoder.cs              # Keypoints protocol decoder
├── SegmentationDecoder.cs           # Segmentation protocol decoder
├── Options/
│   ├── KeypointsOptions.cs
│   └── SegmentationOptions.cs
├── Skeletons/
│   ├── SkeletonDefinition.cs
│   └── SkeletonDefinitions.cs       # COCO pose, etc.
└── ...
```

## Summary

`IRenderingStream` provides:

1. **Clean abstraction** - `IRenderingStream` interface for all streaming protocols
2. **Built-in VectorGraphics** - `RenderingStreams.VectorGraphics()` with improved binary protocol
3. **Pluggable architecture** - Implement `IRenderingStream` or use `RenderingStream` with custom `IFrameDecoder`
4. **Reusable utilities** - `BinaryEncoding` (varint+zigzag) available for custom decoders
5. **Options pattern** - Type-safe configuration
6. **Sample integration** - Ready to use in demos

### Separation of Concerns

| Library | Responsibility |
|---------|---------------|
| **BlazorBlaze** | `IRenderingStream`, `IFrameDecoder`, `VectorGraphicsDecoder`, `BinaryEncoding` |
| **rocket-welder2** | `KeypointsDecoder`, `SegmentationDecoder`, domain-specific options |

This keeps BlazorBlaze as a general-purpose rendering library while allowing rocket-welder2 to implement ML-specific protocol decoders.
