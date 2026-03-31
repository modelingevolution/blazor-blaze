using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Line segment geometry between two points in local coordinates.
/// </summary>
/// <param name="Start">Start point in mm.</param>
/// <param name="End">End point in mm.</param>
public readonly record struct LineGeometry(Point3<double> Start, Point3<double> End) : IGeometry;
