namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Billboard text label that always faces the camera.
/// </summary>
/// <param name="Text">The text to display.</param>
/// <param name="FontSize">Font size in points.</param>
public readonly record struct TextLabelGeometry(string Text, double FontSize) : IGeometry;
