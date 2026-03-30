namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Axis-aligned box geometry defined by width, height, and depth in mm.
/// </summary>
/// <param name="Width">Width along X axis in mm.</param>
/// <param name="Height">Height along Y axis in mm.</param>
/// <param name="Depth">Depth along Z axis in mm.</param>
public readonly record struct BoxGeometry(double Width, double Height, double Depth) : IGeometry;
