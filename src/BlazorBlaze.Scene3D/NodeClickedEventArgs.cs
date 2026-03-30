using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D;

/// <summary>
/// Event arguments for a node click in the 3D scene.
/// Carries enough context for consumers to handle selection, context menus,
/// multi-select (modifier keys), and spatial queries (world position).
/// </summary>
/// <param name="NodeName">Name of the clicked node, or null if no node was hit (background click).</param>
/// <param name="Node">The clicked SceneNode reference, or null if no node was hit.</param>
/// <param name="Button">Mouse button: 0 = left, 1 = middle, 2 = right (matches JS MouseEvent.button).</param>
/// <param name="ClientX">Horizontal position in client (viewport) coordinates.</param>
/// <param name="ClientY">Vertical position in client (viewport) coordinates.</param>
/// <param name="CtrlKey">True if Ctrl was held during the click.</param>
/// <param name="ShiftKey">True if Shift was held during the click.</param>
/// <param name="AltKey">True if Alt was held during the click.</param>
/// <param name="WorldPosition">
/// The 3D hit point in world coordinates, if the renderer supports raycasting.
/// Null when the renderer does not support hit-point computation.
/// </param>
public sealed record NodeClickedEventArgs(
    string? NodeName,
    SceneNode? Node,
    int Button,
    double ClientX,
    double ClientY,
    bool CtrlKey,
    bool ShiftKey,
    bool AltKey,
    Point3<double>? WorldPosition = null);
