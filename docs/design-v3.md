# VectorGraphics Protocol V3 - Multi-threaded Decoder Architecture

## Status: Design Only (Not Implemented)

This document captures the architectural design for a future V3 decoder that addresses performance bottlenecks identified in V2.

## Problem Statement

### Current V2 Architecture (2 Threads)

```
┌─────────────────────────────────────────────┐
│  Decoder Thread                             │
│  WebSocket → Parse → Render to SKBitmap     │
│  (per layer)                                │
└──────────────────┬──────────────────────────┘
                   │ layer bitmaps
                   ▼
┌─────────────────────────────────────────────┐
│  UI Thread                                  │
│  Composite layer bitmaps → Display          │
└─────────────────────────────────────────────┘
```

V2 already separates decoding/rendering from UI composition.

### Bottleneck Analysis

The decoder thread does ALL parsing and rendering sequentially:

| Operation | Cost | Thread |
|-----------|------|--------|
| Protocol parsing (varint, byte copy) | Cheap (nanoseconds) | Decoder |
| JPEG decode (`SKImage.FromEncodedData`) | Expensive (milliseconds) | Decoder |
| Draw operations (`canvas.DrawXYZ`) | Expensive (microseconds-milliseconds) | Decoder |
| Layer composition | Cheap | UI |

For Full HD MJPEG at 30fps (~33ms frame budget), the **single decoder thread** must:
1. Parse all layer data
2. Decode JPEG (10-20ms)
3. Render all vector overlays

This serialization is the bottleneck - layers are processed sequentially even though they're independent.

## Proposed V3 Architecture

### Core Concept: Parallel Layer Pre-rendering

```
┌─────────────────────────────────────────────────────────────┐
│  WebSocket Stream                                           │
│  [Frame N: Layer0 ops | Layer1 ops | Layer2 ops]            │
└───────────────┬─────────────────────────────────────────────┘
                │
                ▼
        ┌───────────────┐
        │  Demultiplexer │
        │  (by layer +   │
        │   by frame)    │
        └───┬───┬───┬───┘
            │   │   │
    ┌───────┘   │   └───────┐
    ▼           ▼           ▼
┌────────┐ ┌────────┐ ┌────────┐
│Layer 0 │ │Layer 1 │ │Layer 2 │
│Pool    │ │Pool    │ │Pool    │
│(2 thrd)│ │(1 thrd)│ │(1 thrd)│
└────────┘ └────────┘ └────────┘
    │           │           │
    ▼           ▼           ▼
┌────────┐ ┌────────┐ ┌────────┐
│Bitmap  │ │Bitmap  │ │Bitmap  │
│Queue   │ │Queue   │ │Queue   │
└────┬───┘ └────┬───┘ └────┬───┘
     │          │          │
     └──────────┼──────────┘
                ▼
┌─────────────────────────────────────────────────────────────┐
│  UI Thread: Composite layer bitmaps                         │
│  canvas.DrawImage(layer0Bitmap, 0, 0);                      │
│  canvas.DrawImage(layer1Bitmap, 0, 0);  // alpha blend      │
│  canvas.DrawImage(layer2Bitmap, 0, 0);                      │
└─────────────────────────────────────────────────────────────┘
```

### Key Design Decisions

#### 1. Demultiplexing by Layer AND Frame

Not just one thread per layer - multiple frames of the same layer can be decoded in parallel:

```
Layer 0 (JPEG - heavy workload):
  Thread A: Frame N   → Decode JPEG → Render to Bitmap
  Thread B: Frame N+1 → Decode JPEG → Render to Bitmap
  Thread A: Frame N+2 → ...

Layer 1 (vectors - light workload):
  Thread C: Frame N   → Decode ops → Render to Bitmap
  Thread C: Frame N+1 → ...
```

This allows scaling parallelism based on layer workload. JPEG layers get more threads.

#### 2. Relaxed Synchronization Between Layers

Layers can be 1-2 frames out of sync. This is visually imperceptible and eliminates the need for barrier synchronization between layer threads.

UI thread picks the **latest ready bitmap** for each layer and composites.

#### 3. Command-Frame Abstraction

Protocol bytes are parsed into command-frame objects - lightweight representations of draw operations with raw data (e.g., JPEG bytes not yet decoded).

Parsing is cheap. The heavy work (JPEG decode, path tessellation) happens during pre-rendering, not parsing.

#### 4. Handling Remain Mode

Remain mode adds state management complexity:

```csharp
class LayerState
{
    SKBitmap LastRenderedBitmap;
    int LastMasterFrameId;
    TaskCompletionSource<SKBitmap> CurrentFrameTcs;
}
```

When Remain is received:
- Don't decode/render
- Complete TCS immediately with cached `LastRenderedBitmap`
- UI thread receives the cached bitmap

When Master/Clear is received:
- Decode and render to new bitmap
- Update `LastRenderedBitmap`
- Complete TCS with new bitmap

#### 5. Bitmap Lifecycle Management

Bitmaps must be retained while Remain mode could reference them:

```
Frame N:   Layer0=Master  → Bitmap A created
Frame N+1: Layer0=Remain  → Bitmap A reused
Frame N+2: Layer0=Remain  → Bitmap A reused
Frame N+3: Layer0=Master  → Bitmap B created, Bitmap A can be disposed
```

Use reference counting or a bitmap pool with generational tracking.

## Components

- **Demultiplexer**: Parses protocol stream, routes command-frames to appropriate layer queues
- **Layer Pre-renderer**: Per-layer thread pool that decodes and renders to bitmap. Configurable thread count based on workload (more threads for JPEG layers)
- **Compositor**: Awaits layer bitmaps (with timeout for lagging layers), composites to final output

## Trade-offs

### Advantages

- Utilizes multi-core CPUs effectively
- JPEG decode (the bottleneck) runs in parallel
- UI thread only does cheap composition
- Scales with layer count and complexity

### Disadvantages

- Significant implementation complexity
- Memory pressure (multiple bitmaps in flight)
- Thread synchronization overhead
- More complex error handling
- Harder to debug

### Memory Estimate

For 1920x1080 RGBA:
- Per bitmap: ~8MB
- 3 layers x 2 frames in flight: ~48MB
- Plus bitmap pool overhead: ~64-96MB total

## Migration Path

1. V2 remains the default (simple, single-threaded)
2. V3 opt-in via configuration for high-performance scenarios
3. Same protocol wire format - only decoder changes

## Open Questions

1. How to handle frame drops when a layer falls too far behind?
2. Should bitmap pool be shared across layers or per-layer?
3. What's the right thread count heuristic per layer type?
4. How to expose performance metrics (decode time, queue depth)?

## References

- Current V2 protocol: `docs/design-protocol-v2.md`
- Similar architecture: Video compositor pipelines (OBS, FFmpeg filter graphs)
