# VectorGraphics Protocol v2 Design

## Overview

Redesign of the binary streaming protocol to mirror SkiaSharp's stateful canvas API. The new protocol separates context/transform state from draw operations, enabling:

1. **Stateful context** - Set once, apply to multiple draws
2. **Save/Restore** - Push/pop context state for hierarchical transforms
3. **Multi-layer compositing** - Each layer renders to separate SKCanvas, composited on EndMarker
4. **Keyframe compression** - Only send changed layers (Master/Remain/Clear)
5. **Smaller wire format** - No repeated context data per draw
6. **Cleaner mental model** - Matches established graphics API patterns

## Multi-Layer Compositing Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Client Renderer                          │
├─────────────────────────────────────────────────────────────┤
│  Layer 0 (background):  [SKCanvas] ─┐                       │
│  Layer 1 (content):     [SKCanvas] ─┼─> Composite ─> Output │
│  Layer 2 (foreground):  [SKCanvas] ─┘   on EndMarker        │
└─────────────────────────────────────────────────────────────┘
```

Each layer is an independent SKCanvas (in-memory bitmap). On EndMarker, layers are composited in z-order onto the final output canvas.

### Layer Frame Types

| Type | Name | Behavior |
|------|------|----------|
| 0x00 | Master | Clear layer canvas, redraw with operations that follow |
| 0x01 | Remain | Keep previous layer content unchanged (no operations follow) |
| 0x02 | Clear | Clear layer canvas to transparent (no operations follow) |

### Keyframe Compression Example

**Frame 1** - Initial full scene:
```
Layer 0: Master (draw static background - 10KB)
Layer 1: Master (draw 100 animated polygons - 5KB)
Layer 2: Master (draw UI overlay - 3KB)
Total: 18KB
```

**Frames 2-59** - Only animated layer changes:
```
Layer 0: Remain (no data sent)
Layer 1: Master (redraw animated polygons - 5KB)
Layer 2: Remain (no data sent)
Total: 5KB per frame
```

**Frame 60** - UI score update:
```
Layer 0: Remain (no data sent)
Layer 1: Master (animated polygons - 5KB)
Layer 2: Master (updated score - 3KB)
Total: 8KB
```

**Bandwidth savings**: Instead of 60 × 18KB = 1080KB, we send 18KB + 58 × 5KB + 8KB = 316KB (70% reduction)

## Protocol Structure

### Message Format

A WebSocket message contains one or more layer frames:

```
Message:
  [GlobalFrameId: 8 bytes LE]   // Monotonically increasing frame counter
  [LayerCount: 1 byte]          // Number of layer blocks (1-255)
  For each layer:
    [LayerBlock...]
  [EndMarker: 0xFF 0xFF]        // Triggers compositing
```

### Layer Block Format

```
LayerBlock:
  [LayerId: 1 byte]             // Layer index (0 = bottom, higher = top)
  [FrameType: 1 byte]           // 0x00=Master, 0x01=Remain, 0x02=Clear
  If FrameType == Master:
    [OpCount: varint]           // Number of operations
    [Operations...]             // Draw and context operations
```

**Remain and Clear have no payload** - just LayerId + FrameType (2 bytes total).

### Operation Types

```
OpType (1 byte):
  0x01 = DrawPolygon
  0x02 = DrawText
  0x03 = DrawCircle
  0x04 = DrawRect
  0x05 = DrawLine
  0x06 = DrawPath (future)
  ...
  0x10 = SetContext        // Set context properties
  0x11 = SaveContext       // Push context onto stack
  0x12 = RestoreContext    // Pop context from stack
  0x13 = ResetContext      // Reset to default values
  ...
  0xFE = Reserved
  0xFF = EndMarker (paired)
```

## Context Operations

### SetContext (0x10)

Sets one or more properties on the current drawing context. Properties not set retain their previous values.

```
SetContext:
  [0x10]                   // OpType
  [FieldCount: varint]     // Number of fields to set
  For each field:
    [PropertyId: 1 byte]   // Which property
    [Value: varies]        // Property-specific encoding
```

**Property IDs and Encodings:**

| ID   | Name      | Encoding                          | Description                    |
|------|-----------|-----------------------------------|--------------------------------|
| 0x01 | Stroke    | 4 bytes (R, G, B, A)              | Stroke color                   |
| 0x02 | Fill      | 4 bytes (R, G, B, A)              | Fill color                     |
| 0x03 | Thickness | varint                            | Stroke width in pixels         |
| 0x04 | FontSize  | varint                            | Font size in pixels            |
| 0x05 | FontColor | 4 bytes (R, G, B, A)              | Text color                     |
| 0x10 | Offset    | 2 x zigzag varint (X, Y)          | Translation in pixels          |
| 0x11 | Rotation  | 4 bytes float LE                  | Rotation in degrees            |
| 0x12 | Scale     | 2 x 4 bytes float LE (X, Y)       | Scale factors                  |
| 0x13 | Skew      | 2 x 4 bytes float LE (X, Y)       | Skew factors                   |
| 0x20 | Matrix    | 6 x 4 bytes float LE              | Full SKMatrix (ScaleX, SkewX, TransX, SkewY, ScaleY, TransY) |

**Example - Set stroke and offset:**
```
[0x10]           // SetContext
[0x02]           // 2 fields
[0x01][255,0,0,255]  // Stroke = red
[0x10][100,100]      // Offset = (100, 100)
```

### SaveContext (0x11)

Pushes the current context state onto a stack. No payload.

```
SaveContext:
  [0x11]  // OpType only
```

### RestoreContext (0x12)

Pops and restores the most recently saved context state. No payload.

```
RestoreContext:
  [0x12]  // OpType only
```

### ResetContext (0x13)

Resets context to default values (no stroke, no fill, identity transform). No payload.

```
ResetContext:
  [0x13]  // OpType only
```

## Draw Operations

Draw operations use the current context state. They no longer carry embedded context.

### DrawPolygon (0x01)

```
DrawPolygon:
  [0x01]                   // OpType
  [PointCount: varint]     // Number of points
  [FirstX: zigzag varint]  // First point X (absolute)
  [FirstY: zigzag varint]  // First point Y (absolute)
  For remaining points:
    [DeltaX: zigzag varint]  // Delta from previous X
    [DeltaY: zigzag varint]  // Delta from previous Y
```

### DrawText (0x02)

```
DrawText:
  [0x02]                   // OpType
  [X: zigzag varint]       // Position X
  [Y: zigzag varint]       // Position Y
  [TextLen: varint]        // UTF-8 byte count
  [TextBytes: UTF-8]       // Text content
```

### DrawCircle (0x03)

```
DrawCircle:
  [0x03]                   // OpType
  [CenterX: zigzag varint] // Center X
  [CenterY: zigzag varint] // Center Y
  [Radius: varint]         // Radius in pixels
```

### DrawRect (0x04)

```
DrawRect:
  [0x04]                   // OpType
  [X: zigzag varint]       // Top-left X
  [Y: zigzag varint]       // Top-left Y
  [Width: varint]          // Width in pixels
  [Height: varint]         // Height in pixels
```

### DrawLine (0x05)

```
DrawLine:
  [0x05]                   // OpType
  [X1: zigzag varint]      // Start X
  [Y1: zigzag varint]      // Start Y
  [X2: zigzag varint]      // End X
  [Y2: zigzag varint]      // End Y
```

## Example Frames

### Simple: 3 red polygons at different positions

```
[0x00][frameId][layerId][5]  // Master frame, 5 ops

[0x10][1][0x01][255,0,0,255] // SetContext: Stroke=red

[0x10][1][0x10][100,100]     // SetContext: Offset=(100,100)
[0x01][200][points...]       // DrawPolygon

[0x10][1][0x10][300,100]     // SetContext: Offset=(300,100)
[0x01][200][points...]       // DrawPolygon

[0x10][1][0x10][500,100]     // SetContext: Offset=(500,100)
[0x01][200][points...]       // DrawPolygon

[0xFF][0xFF]                 // EndMarker
```

### With Save/Restore: Hierarchical transforms

```
[0x00][frameId][layerId][8]  // Master frame, 8 ops

[0x11]                       // SaveContext (save default)

[0x10][2]                    // SetContext: 2 fields
  [0x10][400,300]            //   Offset = center of canvas
  [0x11][45.0f]              //   Rotation = 45 degrees

[0x01][100][points...]       // DrawPolygon (rotated around center)

[0x11]                       // SaveContext (save rotated state)
[0x10][1][0x12][0.5f,0.5f]   // SetContext: Scale = 0.5
[0x01][100][points...]       // DrawPolygon (rotated + scaled)
[0x12]                       // RestoreContext (back to just rotated)

[0x01][100][points...]       // DrawPolygon (just rotated again)

[0x12]                       // RestoreContext (back to default)

[0xFF][0xFF]                 // EndMarker
```

## Client-Side Architecture

### Layer Manager

```csharp
public class LayerManager : IDisposable
{
    private readonly Dictionary<byte, LayerCanvas> _layers = new();
    private readonly SKImageInfo _imageInfo;

    public LayerManager(int width, int height)
    {
        _imageInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    }

    public LayerCanvas GetOrCreateLayer(byte layerId)
    {
        if (!_layers.TryGetValue(layerId, out var layer))
        {
            layer = new LayerCanvas(_imageInfo);
            _layers[layerId] = layer;
        }
        return layer;
    }

    public void ProcessLayerBlock(byte layerId, FrameType frameType, ReadOnlySpan<byte> operations)
    {
        var layer = GetOrCreateLayer(layerId);

        switch (frameType)
        {
            case FrameType.Master:
                layer.Clear();
                layer.DecodeAndRender(operations);
                break;
            case FrameType.Remain:
                // Do nothing - keep existing content
                break;
            case FrameType.Clear:
                layer.Clear();
                break;
        }
    }

    public void Composite(SKCanvas outputCanvas)
    {
        // Composite layers in z-order (0 = bottom)
        foreach (var layerId in _layers.Keys.OrderBy(k => k))
        {
            var layer = _layers[layerId];
            outputCanvas.DrawBitmap(layer.Bitmap, 0, 0);
        }
    }
}
```

### Layer Canvas

Each layer has its own SKCanvas backed by an SKBitmap:

```csharp
public class LayerCanvas : IDisposable
{
    public SKBitmap Bitmap { get; }
    public SKCanvas Canvas { get; }
    public DecoderState State { get; } = new();

    public LayerCanvas(SKImageInfo info)
    {
        Bitmap = new SKBitmap(info);
        Canvas = new SKCanvas(Bitmap);
    }

    public void Clear()
    {
        Canvas.Clear(SKColors.Transparent);
        State.Reset();
    }

    public void DecodeAndRender(ReadOnlySpan<byte> operations)
    {
        // Decode operations and render to this layer's canvas
        // Uses State for context tracking
    }

    public void Dispose()
    {
        Canvas.Dispose();
        Bitmap.Dispose();
    }
}
```

### Decoder State (per-layer)

```csharp
public class DecoderState
{
    public DrawContext Current { get; set; } = DrawContext.Default;
    public Stack<DrawContext> Stack { get; } = new();

    public void Save() => Stack.Push(Current);
    public void Restore() => Current = Stack.Count > 0 ? Stack.Pop() : DrawContext.Default;
    public void Reset()
    {
        Current = DrawContext.Default;
        Stack.Clear();
    }

    public void Set(PropertyId id, object value)
    {
        Current = id switch
        {
            PropertyId.Stroke => Current with { Stroke = (RgbColor)value },
            PropertyId.Fill => Current with { Fill = (RgbColor)value },
            PropertyId.Thickness => Current with { Thickness = (int)value },
            PropertyId.Offset => Current with { Offset = (SKPoint)value },
            PropertyId.Rotation => Current with { Rotation = (float)value },
            PropertyId.Scale => Current with { Scale = (SKPoint)value },
            PropertyId.Skew => Current with { Skew = (SKPoint)value },
            PropertyId.Matrix => Current with { Matrix = (SKMatrix)value },
            _ => Current
        };
    }
}
```

### Rendering Flow

```
WebSocket Message Received
         │
         ▼
┌─────────────────────────┐
│ Parse GlobalFrameId     │
│ Parse LayerCount        │
└─────────────────────────┘
         │
         ▼
┌─────────────────────────┐
│ For each LayerBlock:    │
│  ├─ Parse LayerId       │
│  ├─ Parse FrameType     │
│  └─ ProcessLayerBlock() │◄── Master: Clear + Render
│                         │    Remain: Skip
│                         │    Clear: Clear only
└─────────────────────────┘
         │
         ▼
┌─────────────────────────┐
│ On EndMarker:           │
│  Composite all layers   │
│  to output canvas       │
└─────────────────────────┘
         │
         ▼
    Display Frame
```

## Server-Side IRemoteCanvas Interface

```csharp
public interface IRemoteCanvas : IDisposable
{
    ulong FrameId { get; }

    // Layer control
    ILayerCanvas Layer(byte layerId);           // Get layer for drawing
    ILayerCanvas this[byte layerId] { get; }    // Indexer shortcut

    // Frame control
    void BeginFrame();                          // Start new frame
    ValueTask FlushAsync(CancellationToken ct); // Send frame to client
}

public interface ILayerCanvas
{
    byte LayerId { get; }

    // Layer frame type
    void Master();   // Clear and redraw (default)
    void Remain();   // Keep previous content
    void Clear();    // Clear to transparent

    // Context state
    void SetStroke(RgbColor color);
    void SetFill(RgbColor color);
    void SetThickness(int width);
    void SetFontSize(int size);
    void SetFontColor(RgbColor color);

    // Transform state
    void Translate(float dx, float dy);
    void Rotate(float degrees);
    void Scale(float sx, float sy);
    void Skew(float kx, float ky);
    void SetMatrix(SKMatrix matrix);

    // Context stack
    void Save();
    void Restore();
    void ResetContext();

    // Draw operations (use current context)
    void DrawPolygon(ReadOnlySpan<SKPoint> points);
    void DrawText(string text, int x, int y);
    void DrawCircle(int centerX, int centerY, int radius);
    void DrawRectangle(int x, int y, int width, int height);
    void DrawLine(int x1, int y1, int x2, int y2);
}
```

## Usage Example (Server)

### Basic: Single Layer Animation

```csharp
app.MapVectorGraphicsEndpoint("/ws/stream", async (IRemoteCanvas canvas, CancellationToken ct) =>
{
    var points = CreateStarPoints();

    while (!ct.IsCancellationRequested)
    {
        canvas.BeginFrame();

        var layer = canvas.Layer(0);
        layer.Master();  // Clear and redraw

        // Draw 100 animated stars
        for (int i = 0; i < 100; i++)
        {
            layer.Save();
            layer.Translate(centerX[i], centerY[i]);
            layer.Rotate(time * 60 + i * 3.6f);
            layer.Scale(0.5f + 0.5f * MathF.Sin(time + i * 0.1f));
            layer.SetStroke(colors[i % colors.Length]);
            layer.DrawPolygon(points);
            layer.Restore();
        }

        await canvas.FlushAsync(ct);
        await Task.Delay(16, ct); // ~60 FPS
    }
});
```

### Advanced: Multi-Layer with Keyframe Compression

```csharp
app.MapVectorGraphicsEndpoint("/ws/stream", async (IRemoteCanvas canvas, CancellationToken ct) =>
{
    var starPoints = CreateStarPoints();
    var backgroundDrawn = false;
    var lastScore = -1;
    var score = 0;

    while (!ct.IsCancellationRequested)
    {
        canvas.BeginFrame();

        // Layer 0: Static background (only draw once)
        if (!backgroundDrawn)
        {
            var bg = canvas.Layer(0);
            bg.Master();
            bg.SetFill(new RgbColor(20, 20, 40));
            bg.DrawRectangle(0, 0, 1200, 800);
            DrawStarfield(bg);
            backgroundDrawn = true;
        }
        else
        {
            canvas.Layer(0).Remain();  // Keep previous - no data sent!
        }

        // Layer 1: Animated content (always redrawn)
        var content = canvas.Layer(1);
        content.Master();
        for (int i = 0; i < 100; i++)
        {
            content.Save();
            content.Translate(centerX[i], centerY[i]);
            content.Rotate(time * 60 + i * 3.6f);
            content.SetStroke(colors[i % colors.Length]);
            content.DrawPolygon(starPoints);
            content.Restore();
        }

        // Layer 2: UI overlay (only redraw when score changes)
        if (score != lastScore)
        {
            var ui = canvas.Layer(2);
            ui.Master();
            ui.SetFontSize(24);
            ui.SetFontColor(RgbColor.White);
            ui.DrawText($"Score: {score}", 10, 30);
            lastScore = score;
        }
        else
        {
            canvas.Layer(2).Remain();  // Keep previous UI
        }

        await canvas.FlushAsync(ct);
        await Task.Delay(16, ct);
        time += 0.016f;
    }
});
```

### Wire Format for Multi-Layer Frame

The advanced example above produces:

**Frame 1 (initial):**
```
[GlobalFrameId: 1]
[LayerCount: 3]
  [Layer 0][Master][ops: background + starfield]  // ~10KB
  [Layer 1][Master][ops: 100 animated stars]      // ~5KB
  [Layer 2][Master][ops: score text]              // ~100 bytes
[EndMarker]
Total: ~15KB
```

**Frames 2-59 (animation only):**
```
[GlobalFrameId: N]
[LayerCount: 3]
  [Layer 0][Remain]                               // 2 bytes
  [Layer 1][Master][ops: 100 animated stars]      // ~5KB
  [Layer 2][Remain]                               // 2 bytes
[EndMarker]
Total: ~5KB
```

**Frame 60 (score update):**
```
[GlobalFrameId: 60]
[LayerCount: 3]
  [Layer 0][Remain]                               // 2 bytes
  [Layer 1][Master][ops: 100 animated stars]      // ~5KB
  [Layer 2][Master][ops: score text]              // ~100 bytes
[EndMarker]
Total: ~5.1KB
```

## Migration Path

1. **Phase 1**: Add new operation types to encoder/decoder alongside existing
2. **Phase 2**: Update IRemoteCanvas to use new stateful API
3. **Phase 3**: Update sample app to use new API
4. **Phase 4**: Deprecate old context-per-draw format
5. **Phase 5**: Remove old format support

## Wire Format Size Comparison

**Current (context per draw) - 100 polygons with transform:**
- Per polygon: ~20 bytes context (stroke + offset + rotation + scale)
- Total context overhead: ~2000 bytes/frame

**New (stateful context) - 100 polygons with transform:**
- SetContext for stroke: 1 + 1 + 1 + 4 = 7 bytes (once)
- Per polygon: SetContext(offset, rotation, scale) + DrawPolygon
  - SetContext: 1 + 1 + (1+8) + (1+4) + (1+8) = 25 bytes
  - But offset/rotation/scale could be combined into SetMatrix: 1 + 1 + 1 + 24 = 27 bytes
- If polygons share transform: massive savings

**Worst case (unique transform per polygon)**: Similar size
**Best case (shared transforms)**: 10-50% smaller

## Transform Composition Order

When building SKMatrix from individual transform properties:
```
Matrix = Translate × Rotate × Scale × Skew
```

Applied in code:
```csharp
public SKMatrix BuildMatrix()
{
    var matrix = SKMatrix.Identity;

    if (Offset.HasValue)
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(Offset.Value.X, Offset.Value.Y));

    if (Rotation.HasValue)
        matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(Rotation.Value));

    if (Scale.HasValue)
        matrix = matrix.PostConcat(SKMatrix.CreateScale(Scale.Value.X, Scale.Value.Y));

    if (Skew.HasValue)
        matrix = matrix.PostConcat(SKMatrix.CreateSkew(Skew.Value.X, Skew.Value.Y));

    return matrix;
}
```

## DrawContext Record Update

```csharp
public record struct DrawContext
{
    // Styling
    public RgbColor? Stroke { get; init; }
    public RgbColor? Fill { get; init; }
    public int Thickness { get; init; }
    public int FontSize { get; init; }
    public RgbColor? FontColor { get; init; }

    // Transform (individual components)
    public SKPoint? Offset { get; init; }
    public float? Rotation { get; init; }
    public SKPoint? Scale { get; init; }
    public SKPoint? Skew { get; init; }

    // OR full matrix (takes precedence if set)
    public SKMatrix? Matrix { get; init; }

    public static DrawContext Default => new()
    {
        Thickness = 1,
        FontSize = 12
    };

    public bool HasTransform => Offset.HasValue || Rotation.HasValue ||
                                 Scale.HasValue || Skew.HasValue || Matrix.HasValue;

    public SKMatrix BuildMatrix()
    {
        if (Matrix.HasValue)
            return Matrix.Value;

        var m = SKMatrix.Identity;
        if (Offset.HasValue)
            m = m.PostConcat(SKMatrix.CreateTranslation(Offset.Value.X, Offset.Value.Y));
        if (Rotation.HasValue)
            m = m.PostConcat(SKMatrix.CreateRotationDegrees(Rotation.Value));
        if (Scale.HasValue)
            m = m.PostConcat(SKMatrix.CreateScale(Scale.Value.X, Scale.Value.Y));
        if (Skew.HasValue)
            m = m.PostConcat(SKMatrix.CreateSkew(Skew.Value.X, Skew.Value.Y));
        return m;
    }
}
```
