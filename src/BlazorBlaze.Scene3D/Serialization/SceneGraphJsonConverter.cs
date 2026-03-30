using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorBlaze.Scene3D.Geometries;
using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D.Serialization;

/// <summary>
/// JSON converter for SceneGraph. Serializes the full node tree including poses,
/// geometry, colors, visibility, and hierarchy.
/// </summary>
public sealed class SceneGraphJsonConverter : JsonConverter<SceneGraph>
{
    public override SceneGraph? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var graph = new SceneGraph();
        if (root.TryGetProperty("roots", out var rootsElement))
        {
            foreach (var nodeElement in rootsElement.EnumerateArray())
            {
                var node = graph.AddRoot(nodeElement.GetProperty("name").GetString()!);
                ReadNodeProperties(node, nodeElement, options);
            }
        }

        return graph;
    }

    public override void Write(Utf8JsonWriter writer, SceneGraph value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("roots");
        writer.WriteStartArray();
        foreach (var root in value.Roots)
        {
            WriteNode(writer, root, options);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteNode(Utf8JsonWriter writer, SceneNode node, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", node.Name);

        writer.WritePropertyName("localPose");
        JsonSerializer.Serialize(writer, node.LocalPose, options);

        if (node.Geometry is not null)
        {
            writer.WritePropertyName("geometry");
            JsonSerializer.Serialize(writer, node.Geometry, options);
        }

        writer.WriteString("color", node.Color.ToString());
        writer.WriteNumber("opacity", node.Opacity);
        writer.WriteBoolean("visible", node.Visible);

        if (node.Children.Count > 0)
        {
            writer.WritePropertyName("children");
            writer.WriteStartArray();
            foreach (var child in node.Children)
            {
                WriteNode(writer, child, options);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void ReadNodeProperties(SceneNode node, JsonElement element, JsonSerializerOptions options)
    {
        if (element.TryGetProperty("localPose", out var poseElement))
        {
            node.LocalPose = JsonSerializer.Deserialize<Pose3<double>>(poseElement.GetRawText(), options);
        }

        if (element.TryGetProperty("geometry", out var geoElement))
        {
            node.Geometry = JsonSerializer.Deserialize<IGeometry>(geoElement.GetRawText(), options);
        }

        if (element.TryGetProperty("color", out var colorElement))
        {
            var colorStr = colorElement.GetString();
            if (colorStr is null)
                throw new JsonException("Color value is null.");

            try
            {
                // Use Color.Parse instead of Color.TryParse to work around upstream bug:
                // Color.TryParse does not restore alpha=255 for 6-digit hex strings,
                // while Color.Parse handles this correctly.
                node.Color = Color.Parse(colorStr);
            }
            catch (Exception ex) when (ex is not JsonException)
            {
                throw new JsonException($"Failed to parse color value '{colorStr}'.", ex);
            }
        }

        if (element.TryGetProperty("opacity", out var opacityElement))
        {
            node.Opacity = opacityElement.GetDouble();
        }

        if (element.TryGetProperty("visible", out var visibleElement))
        {
            node.Visible = visibleElement.GetBoolean();
        }

        if (element.TryGetProperty("children", out var childrenElement))
        {
            foreach (var childElement in childrenElement.EnumerateArray())
            {
                var childName = childElement.GetProperty("name").GetString()!;
                var child = node.AddChild(childName);
                ReadNodeProperties(child, childElement, options);
            }
        }
    }
}
