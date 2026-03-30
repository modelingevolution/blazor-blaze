using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D;

/// <summary>
/// Orbit camera for 3D scene viewing. Z-up convention.
/// The camera orbits around a target point at a given distance,
/// controlled by azimuth (horizontal angle) and elevation (vertical angle).
/// </summary>
public sealed class Camera3D
{
    /// <summary>
    /// The point the camera orbits around.
    /// </summary>
    public Point3<double> Target { get; set; } = Point3<double>.Zero;

    /// <summary>
    /// Distance from the target in mm.
    /// </summary>
    public double Distance { get; set; } = 500.0;

    /// <summary>
    /// Horizontal angle in degrees (rotation around Z axis).
    /// </summary>
    public double AzimuthDegrees { get; set; }

    /// <summary>
    /// Vertical angle in degrees (elevation above the XY plane).
    /// Clamped to (-90, 90) to prevent gimbal lock at poles.
    /// </summary>
    public double ElevationDegrees { get; set; }

    /// <summary>
    /// Vertical field of view in degrees.
    /// </summary>
    public double FieldOfViewDegrees { get; set; } = 60.0;

    /// <summary>
    /// Near clipping plane distance in mm.
    /// </summary>
    public double NearPlane { get; set; } = 1.0;

    /// <summary>
    /// Far clipping plane distance in mm.
    /// </summary>
    public double FarPlane { get; set; } = 100_000.0;

    /// <summary>
    /// Computes the camera eye position in world coordinates using Z-up convention.
    /// Azimuth rotates around Z, elevation lifts above the XY plane.
    /// </summary>
    public Point3<double> ComputeEyePosition()
    {
        var azRad = AzimuthDegrees * Math.PI / 180.0;
        var elRad = ElevationDegrees * Math.PI / 180.0;

        var cosEl = Math.Cos(elRad);
        var x = Target.X + Distance * cosEl * Math.Cos(azRad);
        var y = Target.Y + Distance * cosEl * Math.Sin(azRad);
        var z = Target.Z + Distance * Math.Sin(elRad);

        return new Point3<double>(x, y, z);
    }

    /// <summary>
    /// Computes the normalized view direction (from eye toward target).
    /// </summary>
    public Vector3<double> ComputeViewDirection()
    {
        var eye = ComputeEyePosition();
        var dx = Target.X - eye.X;
        var dy = Target.Y - eye.Y;
        var dz = Target.Z - eye.Z;
        var len = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        if (len < 1e-12) return new Vector3<double>(0, 0, -1);

        return new Vector3<double>(dx / len, dy / len, dz / len);
    }
}
