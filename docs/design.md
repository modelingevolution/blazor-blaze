# VectorGraphics Protocol V2 - Lock-Free Rendering Design

## Problem Statement

The decoder thread receives WebSocket data at up to 60fps and renders to SKBitmap layers.
The UI thread composites these layers at its own rate (potentially 30fps or lower).

Traditional approach with `lock(_sync)` causes:
- Decoder blocks waiting for UI to finish compositing
- UI blocks waiting for decoder to finish drawing
- Frame rate coupling - both threads limited by the slower one

## Solution: Immutable Frame Snapshots with Reference Counting

### Core Concept

```
Frame = RefArray<Lease<LayerCanvas>> = immutable snapshot of layer references
```

- Decoder builds frames by collecting layer references
- On `OnFrameEnd`, snapshot becomes the display frame
- Renderer calls `TryCopy()` to get its own copy with incremented ref counts
- Renderer holds its copy until a NEW frame is available
- Decoder and renderer run at independent frame rates

### Architecture

```
Decoder Thread @ 60fps                    Renderer Thread @ 30fps
       |                                         |
   Frame N                                       |
     Layer 0: Clear → pool.Rent()                |
     Layer 1: Remain → prevFrame[1].TryCopy()    |
     Layer 2: Clear → pool.Rent()                |
   OnFrameEnd                                    |
     prevFrame.Dispose()                         |
     _displayFrame = newFrame                    |
     prevFrame = newFrame                        |
       |                                         |
   Frame N+1                                 RenderLoop:
     Layer 0: Remain                           _displayFrame.TryCopy(out newCopy)
     Layer 1: Clear                            rendererCopy?.Dispose()  // OLD
     Layer 2: Remain                           rendererCopy = newCopy
   OnFrameEnd                                  Composite(rendererCopy)
     prevFrame.Dispose()                         |
     _displayFrame = newFrame ──────────────────>|
       |                                         |
   Frame N+2                                 RenderLoop:
     ...                                       _displayFrame.TryCopy(out newCopy)
       |                                       rendererCopy?.Dispose()
   Frame N+3                                   rendererCopy = newCopy
     ...                                       Composite(rendererCopy)
       |                                         |
   (decoder faster, frames skipped)          (renderer gets latest available)
```

### Key Design Decisions

1. **Renderer holds copy until new frame available**
   - `TryCopy()` increments ref counts, returns new RefArray
   - Old copy disposed ONLY when new copy acquired
   - Renderer can re-render same frame if decoder is slow

2. **Frame rate decoupling**
   - Decoder can run at 60fps, renderer at 30fps, or the other way round.
   - Frames may be skipped - that's acceptable
   - Layers stay alive as long as any holder references them

3. **Reference counting via Ref<T>**
   - Each `Ref<T>` tracks how many holders share the resource
   - `TryCopy()` atomically increments count (CAS loop)
   - `Dispose()` decrements count, returns to pool when 0

4. **Pool integration via Lease<T>**
   - `Lease<T>` wraps pooled item
   - On dispose, returns to pool (not destroy)
   - Clean separation: pool logic in Lease, sharing logic in Ref

### Layer Handling by FrameType

| FrameType | Action |
|-----------|--------|
| `Master`  | `pool.Rent()` → new `Lease` → new `Ref` → add to frame |
| `Clear`   | `pool.Rent()` → new `Lease` → new `Ref` → add to frame |
| `Remain`  | `prevFrame[layerId].TryCopy()` → add to frame (refcount++) |

---

## Type Stack

```
┌─────────────────────────────────────────────────────────────┐
│  RefArray<Lease<LayerCanvas>>  = Frame Snapshot             │
│  ├── Ref<Lease<LayerCanvas>>  (Layer 0)                     │
│  ├── Ref<Lease<LayerCanvas>>  (Layer 1)                     │
│  └── Ref<Lease<LayerCanvas>>  (Layer 2)                     │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│  Ref<Lease<LayerCanvas>>  = Reference-counted layer         │
│  - RefCount: number of frames/copies holding this layer     │
│  - TryCopy(): atomically increment if not disposed          │
│  - Dispose(): decrement, when 0 → Lease.Dispose()           │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│  Lease<LayerCanvas>  = Pool ownership wrapper               │
│  - Value: the underlying LayerCanvas                        │
│  - Dispose(): returns LayerCanvas to pool (not destroy)     │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│  LayerCanvas  = Pooled resource (expensive, reusable)       │
│  - SKBitmap: pixel buffer                                   │
│  - SKCanvas: drawing surface                                │
│  - Clear(): reset for reuse                                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Components

### Ref<T> - Reference-Counted Pointer

```csharp
public sealed class Ref<T> : IDisposable where T : class
{
    private readonly T _value;
    private readonly Action<T>? _returnToPool;
    private int _refCount;

    public Ref(T value, Action<T>? returnToPool = null);

    public T Value { get; }
    public int RefCount { get; }

    /// <summary>
    /// Atomically increment refcount via CAS loop.
    /// Returns false if already disposed (refcount <= 0).
    /// </summary>
    public bool TryCopy(out Ref<T>? copy)
    {
        while (true)
        {
            int current = Volatile.Read(ref _refCount);
            if (current <= 0) { copy = null; return false; }
            if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
            {
                copy = this;  // Same instance, incremented count
                return true;
            }
        }
    }

    /// <summary>
    /// Decrement refcount. When 0, return to pool or dispose.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Decrement(ref _refCount) == 0)
        {
            if (_returnToPool != null) _returnToPool(_value);
            else if (_value is IDisposable d) d.Dispose();
        }
    }
}
```

### RefArray<T> - Frame Snapshot

```csharp
public struct RefArray<T> : IDisposable where T : class, IDisposable
{
    private readonly ImmutableArray<Ref<T>> _array;
    private bool _disposed;
    private SpinLock _lock;

    public RefArray(ImmutableArray<Ref<T>> layers);

    public int Length { get; }
    public T this[int index] { get; }  // Access layer value (no ref count change)

    /// <summary>
    /// Increment ref count on ALL layers, return new RefArray with same refs.
    /// Returns false if disposed. Throws if any TryCopy fails (bug).
    /// </summary>
    public bool TryCopy(out RefArray<T>? copy);

    /// <summary>
    /// Decrement ref count on ALL layers. Idempotent.
    /// </summary>
    public void Dispose();
}
```

### Lease<T> - Pool Return Wrapper

```csharp
public sealed class Lease<T> : IDisposable where T : class
{
    private readonly T _value;
    private readonly Action<T> _returnToPool;
    private int _disposed;

    public Lease(T value, Action<T> returnToPool);

    public T Value { get; }
    public bool IsDisposed { get; }

    /// <summary>
    /// Return item to pool. Idempotent.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _returnToPool(_value);
    }
}
```

### LayerCanvasPool

```csharp
public sealed class LayerCanvasPool
{
    private readonly ConcurrentBag<LayerCanvas> _pool = new();
    private readonly int _width, _height;

    public Lease<LayerCanvas> Rent()
    {
        if (!_pool.TryTake(out var canvas))
            canvas = new LayerCanvas(_width, _height);
        canvas.Clear();
        return new Lease<LayerCanvas>(canvas, Return);
    }

    private void Return(LayerCanvas canvas) => _pool.Add(canvas);
}
```

---

## IStage Interface

IStage is the central component that manages frame lifecycle and provides thread-safe access for the renderer.

```csharp
public interface IStage
{
    /// <summary>
    /// Gets the canvas for the specified layer (for drawing operations).
    /// </summary>
    ICanvas this[byte layerId] { get; }

    /// <summary>
    /// Called when a new frame starts.
    /// </summary>
    void OnFrameStart(ulong frameId);

    /// <summary>
    /// Called when the frame ends. Publishes the frame for renderer.
    /// </summary>
    void OnFrameEnd();

    /// <summary>
    /// Mark layer as Remain - reuse from previous frame.
    /// </summary>
    void Remain(byte layerId);

    /// <summary>
    /// Mark layer as Clear - rent new layer from pool.
    /// </summary>
    void Clear(byte layerId);

    /// <summary>
    /// Called by renderer thread to get a copy of the latest complete frame.
    /// Thread-safe. Increments ref counts on all layers.
    /// </summary>
    bool TryCopyFrame(out RefArray<Lease<ILayer>>? copy);
}
```

## ILayer Interface

```csharp
public interface ILayer : IDisposable
{
    byte LayerId { get; }
    ICanvas Canvas { get; }  // Protocol.ICanvas - abstraction over SKCanvas
    void Clear();
    void DrawTo(SKCanvas target);
}
```

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           VectorGraphicsDecoderV2                           │
│                         (Protocol Parser - Decoder Thread)                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ calls
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              IStage (RenderingStage)                        │
│                         (Frame Manager - Central Component)                 │
│                                                                             │
│  Decoder Thread API:                    Renderer Thread API:                │
│  ├── OnFrameStart(frameId)              └── TryCopyFrame(out copy)          │
│  ├── Clear(layerId)                         (thread-safe, increments refs)  │
│  ├── Remain(layerId)                                                        │
│  ├── this[layerId] → ICanvas                                                │
│  └── OnFrameEnd()                                                           │
│                                                                             │
│  Internal State:                                                            │
│  ├── _workingLayers: Ref<Lease<ILayer>>?[16] (index = layerId, O(1))        │
│  ├── _displayFrame: RefArray<Lease<ILayer>>  (published frame)              │
│  ├── _prevFrame: RefArray<Lease<ILayer>>     (for Remain lookups)           │
│  └── _frameLock: object                      (protects _displayFrame)       │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                    ┌─────────────────┴─────────────────┐
                    │                                   │
                    ▼                                   ▼
┌───────────────────────────────┐     ┌───────────────────────────────────────┐
│      LayerCanvasPool          │     │            Renderer (UI Thread)       │
│  (Pooled ILayer instances)    │     │                                       │
│                               │     │  _rendererCopy: RefArray<...>?        │
│  Rent() → Lease<ILayer>       │     │                                       │
│  Return(layer)                │     │  Loop:                                │
└───────────────────────────────┘     │    stage.TryCopyFrame(out newCopy)    │
                                      │    _rendererCopy?.Dispose()           │
                                      │    _rendererCopy = newCopy            │
                                      │    Composite(_rendererCopy)           │
                                      └───────────────────────────────────────┘
```

## RenderingStage Implementation

```csharp
public class RenderingStage : IStage
{
    private readonly ILayerPool _pool;

    // Working state - index = layerId, O(1) access, no sorting needed
    private readonly Ref<Lease<ILayer>>?[] _workingLayers = new Ref<Lease<ILayer>>?[16];

    private readonly object _frameLock = new();
    private RefArray<Lease<ILayer>> _displayFrame;
    private RefArray<Lease<ILayer>> _prevFrame;

    // --- Decoder Thread API ---

    public void OnFrameStart(ulong frameId)
    {
        Array.Clear(_workingLayers);
    }

    public void Clear(byte layerId)
    {
        var lease = _pool.Rent(layerId);
        lease.Value.Clear();
        var layerRef = new Ref<Lease<ILayer>>(lease, l => l.Dispose());
        _workingLayers[layerId] = layerRef;
    }

    public void Remain(byte layerId)
    {
        var prevRef = _prevFrame.GetRef(layerId);
        if (prevRef == null || !prevRef.TryCopy(out var copy))
            throw new InvalidOperationException($"Remain failed for layer {layerId}");
        _workingLayers[layerId] = copy!;
    }

    public ICanvas this[byte layerId] => GetOrCreateCanvas(layerId);

    public void OnFrameEnd()
    {
        // Build ImmutableArray directly from working array (already ordered by index)
        var builder = ImmutableArray.CreateBuilder<Ref<Lease<ILayer>>?>(_workingLayers.Length);
        for (int i = 0; i < _workingLayers.Length; i++)
            builder.Add(_workingLayers[i]);

        var newFrame = new RefArray<Lease<ILayer>>(builder.MoveToImmutable());

        // Thread-safe publish
        RefArray<Lease<ILayer>> toDispose;
        lock (_frameLock)
        {
            toDispose = _prevFrame;
            _displayFrame = newFrame;
        }
        _prevFrame = newFrame;

        // Dispose outside lock
        toDispose.Dispose();
    }

    // --- Renderer Thread API ---

    public bool TryCopyFrame(out RefArray<Lease<ILayer>>? copy)
    {
        lock (_frameLock)
        {
            return _displayFrame.TryCopy(out copy);
        }
    }
}
```

## Renderer Pattern

```csharp
public class Renderer
{
    private readonly IStage _stage;
    private RefArray<Lease<ILayer>>? _rendererCopy;

    public void RenderLoop()
    {
        while (running)
        {
            // Try to get new frame from stage
            if (_stage.TryCopyFrame(out var newCopy))
            {
                // Got new frame - dispose OLD copy, hold NEW copy
                _rendererCopy?.Dispose();
                _rendererCopy = newCopy;
            }

            // Render current copy (may render same frame multiple times)
            if (_rendererCopy.HasValue)
            {
                var frame = _rendererCopy.Value;
                for (int i = 0; i < frame.Length; i++)
                    frame[i].DrawTo(canvas);
            }
        }
    }

    public void Shutdown()
    {
        _rendererCopy?.Dispose();
        _rendererCopy = null;
    }
}
```

**Key: Renderer calls `IStage.TryCopyFrame()` - the lock ensures atomic read + TryCopy.**

---

## Thread Safety

| Operation | Thread | Synchronization |
|-----------|--------|-----------------|
| `OnFrameStart`, `Clear`, `Remain` | Decoder | None (single writer) |
| `this[layerId]` (get canvas) | Decoder | None (single writer) |
| `OnFrameEnd` (publish) | Decoder | `_frameLock` for _displayFrame |
| `TryCopyFrame` | Renderer | `_frameLock` + CAS in Ref |
| `rendererCopy.Dispose()` | Renderer | SpinLock + atomic decrement |
| Pool rent/return | Both | ConcurrentBag (lock-free) |

---

## Detailed Flow: 3 Layers with Master, Remain, Clear

```
LEGEND:
  L0, L1, L2 = Layer bitmaps (pooled resources)
  (n) = ref count
  ✓pool = returned to pool
  rendererCopy = renderer's held snapshot

================================================================================
FRAME 0: Initial frame - all layers new
================================================================================

Decoder:
  Layer 0: Master  → L0 = pool.Rent(), new Ref(L0)     → L0(1)
  Layer 1: Clear   → L1 = pool.Rent(), new Ref(L1)     → L1(1)
  Layer 2: Clear   → L2 = pool.Rent(), new Ref(L2)     → L2(1)

  frame0 = new RefArray([L0(1), L1(1), L2(1)])
  _displayFrame = frame0
  prevFrame = frame0

                                        Renderer:
                                          _displayFrame.TryCopy(out newCopy)  ✓
                                            → L0(1→2), L1(1→2), L2(1→2)
                                          rendererCopy?.Dispose()  // null, no-op
                                          rendererCopy = newCopy

                                          // Composite layers 0,1,2 to canvas

================================================================================
FRAME 1: Layer 0 Remain, Layer 1 Clear, Layer 2 Remain
================================================================================

Decoder:
  Layer 0: Remain  → prevFrame[0].TryCopy()            → L0(2→3)
  Layer 1: Clear   → L1b = pool.Rent(), new Ref(L1b)   → L1b(1)
  Layer 2: Remain  → prevFrame[2].TryCopy()            → L2(2→3)

  frame1 = new RefArray([L0(3), L1b(1), L2(3)])

  prevFrame.Dispose()
    → L0(3→2), L1(2→1), L2(3→2)

  _displayFrame = frame1
  prevFrame = frame1

                                        // Renderer still compositing...
                                        // rendererCopy still holds [L0(2), L1(1), L2(2)]

================================================================================
FRAME 2: Decoder faster than renderer
================================================================================

Decoder:
  Layer 0: Remain  → prevFrame[0].TryCopy()            → L0(2→3)
  Layer 1: Remain  → prevFrame[1].TryCopy()            → L1b(1→2)
  Layer 2: Clear   → L2b = pool.Rent(), new Ref(L2b)   → L2b(1)

  frame2 = new RefArray([L0(3), L1b(2), L2b(1)])

  prevFrame.Dispose()
    → L0(3→2), L1b(2→1), L2(2→1)

  _displayFrame = frame2
  prevFrame = frame2

                                        // Renderer still on frame0 copy...
                                        // Frames 1 and 2 were "skipped"

================================================================================
RENDERER READY: Gets new frame, disposes old
================================================================================

                                        Renderer:
                                          _displayFrame.TryCopy(out newCopy)  ✓
                                            → L0(2→3), L1b(1→2), L2b(1→2)

                                          rendererCopy.Dispose()  // OLD (frame0)
                                            → L0(3→2), L1(1→0 ✓pool), L2(1→0 ✓pool)

                                          rendererCopy = newCopy

                                          // Composite using frame2

================================================================================
STATE AFTER RENDERER UPDATE
================================================================================

Active refs:
  L0(2)  - held by: prevFrame (frame2), rendererCopy
  L1b(2) - held by: prevFrame (frame2), rendererCopy
  L2b(2) - held by: prevFrame (frame2), rendererCopy

Returned to pool:
  L1  - disposed when rendererCopy (frame0) was disposed
  L2  - disposed when rendererCopy (frame0) was disposed

================================================================================
FRAME 3: All layers Clear (full redraw)
================================================================================

Decoder:
  Layer 0: Clear   → L0b = pool.Rent()                 → L0b(1)
  Layer 1: Clear   → L1c = pool.Rent()                 → L1c(1)
  Layer 2: Clear   → L2c = pool.Rent()                 → L2c(1)

  frame3 = new RefArray([L0b(1), L1c(1), L2c(1)])

  prevFrame.Dispose()
    → L0(2→1), L1b(2→1), L2b(2→1)
    // Still alive - rendererCopy holds them

  _displayFrame = frame3
  prevFrame = frame3

                                        Renderer:
                                          _displayFrame.TryCopy(out newCopy)  ✓
                                            → L0b(1→2), L1c(1→2), L2c(1→2)

                                          rendererCopy.Dispose()  // frame2 copy
                                            → L0(1→0 ✓pool), L1b(1→0 ✓pool), L2b(1→0 ✓pool)

                                          rendererCopy = newCopy

================================================================================
LAYER LIFECYCLE SUMMARY
================================================================================

  L0:  Frame0 ──Remain──> Frame1 ──Remain──> Frame2 ──────> pool (renderer disposed)
  L1:  Frame0 ──────────────────────────────────────────────> pool (renderer disposed)
  L1b: Frame1 ──Remain──> Frame2 ──────────────────────────> pool (renderer disposed)
  L2:  Frame0 ──Remain──> Frame1 ──────────────────────────> pool (renderer disposed)
  L2b: Frame2 ──────────────────────────────────────────────> pool (renderer disposed)
```

---

## Key Scenarios

| Scenario | Action |
|----------|--------|
| **Clear/Master** | `pool.Rent()` → `Lease` → `Ref` → add to frame |
| **Remain** | `prevFrame[layerId].TryCopy()` → add to frame (refcount++) |
| **Layer removed** | Not in new frame → refcount-- → when 0 → pool |
| **Renderer copy** | `displayFrame.TryCopy()` → all refcount++ → safe hold |
| **Frame skip** | Decoder faster → renderer gets latest when ready |

---

## Benefits

1. **Lock-free rendering** - no contention between decoder and renderer
2. **Frame rate independence** - each thread runs at its natural rate
3. **Memory efficiency** - bitmap pooling reduces allocations
4. **Consistent frames** - renderer always sees complete frame
5. **Simple cleanup** - layers return to pool automatically when unreferenced
6. **Frame skipping** - renderer skips frames when slower than decoder
