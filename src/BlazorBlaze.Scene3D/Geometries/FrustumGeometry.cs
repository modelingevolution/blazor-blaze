namespace BlazorBlaze.Scene3D.Geometries;

/// <summary>
/// Camera frustum geometry defined by field of view, aspect ratio, and depth range.
/// </summary>
/// <param name="FovDegrees">Vertical field of view in degrees.</param>
/// <param name="AspectRatio">Width / height ratio.</param>
/// <param name="NearPlane">Near plane distance in mm.</param>
/// <param name="FarPlane">Far plane distance in mm.</param>
public readonly record struct FrustumGeometry(double FovDegrees, double AspectRatio, double NearPlane, double FarPlane) : IGeometry;
