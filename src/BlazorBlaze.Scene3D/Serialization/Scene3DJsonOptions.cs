using System.Text.Json;

namespace BlazorBlaze.Scene3D.Serialization;

/// <summary>
/// Provides pre-configured JsonSerializerOptions for Scene3D types.
/// </summary>
public static class Scene3DJsonOptions
{
    /// <summary>
    /// Default options with all Scene3D converters registered.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = Create();

    /// <summary>
    /// Creates a new JsonSerializerOptions with Scene3D converters.
    /// </summary>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new GeometryJsonConverter());
        options.Converters.Add(new SceneGraphJsonConverter());
        return options;
    }
}
