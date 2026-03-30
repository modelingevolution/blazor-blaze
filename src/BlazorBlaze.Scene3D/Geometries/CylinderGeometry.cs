namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Cylinder geometry defined by radius and height in mm.
/// The cylinder is centered at the node's local origin, extending along the local Z axis.
/// </summary>
/// <param name="Radius">Radius in mm.</param>
/// <param name="Height">Height in mm.</param>
public readonly record struct CylinderGeometry(double Radius, double Height) : IGeometry;
