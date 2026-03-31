namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Ground grid geometry on the XY plane.
/// </summary>
/// <param name="Size">Total size of the grid in mm (square).</param>
/// <param name="CellSize">Size of each grid cell in mm.</param>
public readonly record struct GridGeometry(double Size, double CellSize) : IGeometry;
