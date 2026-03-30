using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Triangle mesh geometry defined by vertices and triangle indices.
/// </summary>
/// <param name="Vertices">Array of vertex positions.</param>
/// <param name="Indices">Triangle indices (every 3 consecutive indices form a triangle).</param>
public sealed record MeshGeometry(Point3<double>[] Vertices, int[] Indices) : IGeometry;
