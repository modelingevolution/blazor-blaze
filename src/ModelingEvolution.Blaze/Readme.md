# BlazorBlaze

A high-performance canvas rendering library for Blazor applications, providing a flexible scene graph system with built-in camera controls, event handling, and shape primitives.

## Features

- **Scene Graph Management**
  - Hierarchical object organization
  - Z-index layering
  - Parent-child relationships
  - Visibility control

- **Camera System**
  - Pan and zoom operations
  - View bounds management
  - Matrix transformations
  - Browser-to-world coordinate mapping

- **Event System**
  - Mouse events (click, move, wheel)
  - Keyboard events
  - Event bubbling
  - Custom cursors
  - Drag and drop support

- **Shape Controls**
  - Rectangle
  - Circle
  - Line
  - Polygon
  - Bitmap rendering

- **Extension System**
  - Modular functionality
  - Built-in extensions for common operations
  - Custom extension support

## Installation

```xml
<PackageReference Include="BlazorBlaze" Version="1.0.0" />
```

## Quick Start

### Basic Setup

```razor
@using BlazorBlaze

<BlazorCanvas Engine="@_engine" Size="@Sizes.FullHD" />

@code {
    private BlazeEngine _engine;

    protected override void OnInitialized()
    {
        _engine = new BlazeEngine(Sizes.FullHD);
    }
}
```

### Adding Shapes

```csharp
// Create a circle
var circle = new CircleControl(new SKPoint(100, 100), 50);
circle.Fill = SKColors.Blue;
circle.Stroke = SKColors.Black;
circle.StrokeWidth = 2;
_engine.Scene.AddControl(circle);

// Create a rectangle
var rect = new RectangleControl(new SKRect(200, 200, 300, 300));
rect.Fill = SKColors.Red;
_engine.Scene.AddControl(rect);
```

### Handling Events

```csharp
circle.OnMouseDown += (sender, e) => {
    circle.Fill = SKColors.Green;
};

circle.OnMouseEnter += (sender, e) => {
    circle.Cursor.Type = MouseCursorType.Pointer;
};
```

### Using Camera Controls

```csharp
// Pan the camera
_engine.Scene.Camera.Move(new SKPoint(100, 0));

// Zoom at a specific point
_engine.Scene.Camera.ZoomAtPoint(1.5f, new SKPoint(400, 300));
```

### Creating Custom Extensions

```csharp
public class CustomExtension : IEngineExtension
{
    private BlazeEngine _engine;

    public void Bind(BlazeEngine engine)
    {
        _engine = engine;
        _engine.Scene.Root.OnMouseDown += OnMouseDown;
    }

    public void Unbind(BlazeEngine engine)
    {
        _engine.Scene.Root.OnMouseDown -= OnMouseDown;
    }

    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        // Custom logic
    }
}

// Enable the extension
_engine.Extensions.Enable<CustomExtension>();
```

### Drag and Drop

```csharp
var draggableCircle = new CircleControl(new SKPoint(150, 150), 30);
draggableCircle.EnableDrag();
_engine.Scene.AddControl(draggableCircle);
```

## Advanced Example: Interactive Drawing Tool

```csharp
public class DrawingTool : IEngineExtension
{
    private BlazeEngine _engine;
    private List<LineControl> _lines = new();
    private SKPoint? _lastPoint;

    public void Bind(BlazeEngine engine)
    {
        _engine = engine;
        engine.Scene.Root.OnMouseDown += OnMouseDown;
        engine.Scene.Root.OnMouseMove += OnMouseMove;
    }

    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        _lastPoint = e.WorldAbsoluteLocation;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_lastPoint == null) return;

        var line = new LineControl(_lastPoint.Value, e.WorldAbsoluteLocation);
        line.Stroke = SKColors.Black;
        line.StrokeWidth = 2;
        _engine.Scene.AddControl(line);
        _lines.Add(line);

        _lastPoint = e.WorldAbsoluteLocation;
    }

    public void Clear()
    {
        foreach (var line in _lines)
            _engine.Scene.RemoveControl(line);
        _lines.Clear();
    }

    public void Unbind(BlazeEngine engine)
    {
        engine.Scene.Root.OnMouseDown -= OnMouseDown;
        engine.Scene.Root.OnMouseMove -= OnMouseMove;
        Clear();
    }
}
```

## Performance Considerations

- Use `IsVisible` property to hide unused controls
- Implement object pooling for frequently created/destroyed objects
- Use `ZIndex` effectively to minimize redraw operations
- Consider using `BrowserStrokeWidth` for consistent stroke sizes across zoom levels

## License

MIT License