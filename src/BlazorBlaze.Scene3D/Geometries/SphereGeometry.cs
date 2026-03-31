namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Sphere geometry defined by radius in mm.
/// </summary>
/// <param name="Radius">Radius in mm.</param>
public readonly record struct SphereGeometry(double Radius) : IGeometry;
