namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// World coordinate axes (RGB = XYZ) rendered at the scene origin.
/// </summary>
/// <param name="Length">Length of each axis line in mm.</param>
public readonly record struct CoordinateAxesGeometry(double Length) : IGeometry;
