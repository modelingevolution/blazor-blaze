namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Local coordinate system overlay (RGB = XYZ axes) at a specific node,
/// showing local frame orientation. Used at TCP frames and waypoints.
/// </summary>
/// <param name="Length">Length of each axis line in mm.</param>
public readonly record struct CoordinateSystemOverlayGeometry(double Length) : IGeometry;
