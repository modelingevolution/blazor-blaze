using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorBlaze.Scene3D.Geometries;
using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D.Serialization;

/// <summary>
/// Polymorphic JSON converter for IGeometry types.
/// Serializes with a "$type" discriminator.
/// </summary>
public sealed class GeometryJsonConverter : JsonConverter<IGeometry>
{
    private const string TypeDiscriminator = "$type";

    public override IGeometry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty(TypeDiscriminator, out var typeProp))
            throw new JsonException("Missing $type discriminator in geometry JSON.");

        var typeName = typeProp.GetString();

        return typeName switch
        {
            nameof(BoxGeometry) => DeserializeBox(root),
            nameof(CylinderGeometry) => DeserializeCylinder(root),
            nameof(SphereGeometry) => DeserializeSphere(root),
            nameof(LineGeometry) => DeserializeLine(root),
            nameof(GridGeometry) => DeserializeGrid(root),
            nameof(FrustumGeometry) => DeserializeFrustum(root),
            nameof(TextLabelGeometry) => DeserializeTextLabel(root),
            nameof(CoordinateAxesGeometry) => DeserializeCoordinateAxes(root),
            nameof(CoordinateSystemOverlayGeometry) => DeserializeCoordinateSystemOverlay(root),
            nameof(MeshGeometry) => DeserializeMesh(root, options),
            _ => throw new JsonException($"Unknown geometry type: {typeName}")
        };
    }

    public override void Write(Utf8JsonWriter writer, IGeometry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case BoxGeometry b:
                writer.WriteString(TypeDiscriminator, nameof(BoxGeometry));
                writer.WriteNumber("width", b.Width);
                writer.WriteNumber("height", b.Height);
                writer.WriteNumber("depth", b.Depth);
                break;

            case CylinderGeometry c:
                writer.WriteString(TypeDiscriminator, nameof(CylinderGeometry));
                writer.WriteNumber("radius", c.Radius);
                writer.WriteNumber("height", c.Height);
                break;

            case SphereGeometry s:
                writer.WriteString(TypeDiscriminator, nameof(SphereGeometry));
                writer.WriteNumber("radius", s.Radius);
                break;

            case LineGeometry l:
                writer.WriteString(TypeDiscriminator, nameof(LineGeometry));
                writer.WritePropertyName("start");
                WritePoint3(writer, l.Start);
                writer.WritePropertyName("end");
                WritePoint3(writer, l.End);
                break;

            case GridGeometry g:
                writer.WriteString(TypeDiscriminator, nameof(GridGeometry));
                writer.WriteNumber("size", g.Size);
                writer.WriteNumber("cellSize", g.CellSize);
                break;

            case FrustumGeometry f:
                writer.WriteString(TypeDiscriminator, nameof(FrustumGeometry));
                writer.WriteNumber("fovDegrees", f.FovDegrees);
                writer.WriteNumber("aspectRatio", f.AspectRatio);
                writer.WriteNumber("nearPlane", f.NearPlane);
                writer.WriteNumber("farPlane", f.FarPlane);
                break;

            case TextLabelGeometry t:
                writer.WriteString(TypeDiscriminator, nameof(TextLabelGeometry));
                writer.WriteString("text", t.Text);
                writer.WriteNumber("fontSize", t.FontSize);
                break;

            case CoordinateAxesGeometry a:
                writer.WriteString(TypeDiscriminator, nameof(CoordinateAxesGeometry));
                writer.WriteNumber("length", a.Length);
                break;

            case CoordinateSystemOverlayGeometry o:
                writer.WriteString(TypeDiscriminator, nameof(CoordinateSystemOverlayGeometry));
                writer.WriteNumber("length", o.Length);
                break;

            case MeshGeometry m:
                writer.WriteString(TypeDiscriminator, nameof(MeshGeometry));
                writer.WritePropertyName("vertices");
                JsonSerializer.Serialize(writer, m.Vertices, options);
                writer.WritePropertyName("indices");
                JsonSerializer.Serialize(writer, m.Indices, options);
                break;

            default:
                throw new JsonException($"Unknown geometry type: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }

    private static void WritePoint3(Utf8JsonWriter writer, Point3<double> pt)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(pt.X);
        writer.WriteNumberValue(pt.Y);
        writer.WriteNumberValue(pt.Z);
        writer.WriteEndArray();
    }

    private static Point3<double> ReadPoint3(JsonElement element)
    {
        return new Point3<double>(
            element[0].GetDouble(),
            element[1].GetDouble(),
            element[2].GetDouble());
    }

    private static BoxGeometry DeserializeBox(JsonElement root) =>
        new(root.GetProperty("width").GetDouble(),
            root.GetProperty("height").GetDouble(),
            root.GetProperty("depth").GetDouble());

    private static CylinderGeometry DeserializeCylinder(JsonElement root) =>
        new(root.GetProperty("radius").GetDouble(),
            root.GetProperty("height").GetDouble());

    private static SphereGeometry DeserializeSphere(JsonElement root) =>
        new(root.GetProperty("radius").GetDouble());

    private static LineGeometry DeserializeLine(JsonElement root) =>
        new(ReadPoint3(root.GetProperty("start")),
            ReadPoint3(root.GetProperty("end")));

    private static GridGeometry DeserializeGrid(JsonElement root) =>
        new(root.GetProperty("size").GetDouble(),
            root.GetProperty("cellSize").GetDouble());

    private static FrustumGeometry DeserializeFrustum(JsonElement root) =>
        new(root.GetProperty("fovDegrees").GetDouble(),
            root.GetProperty("aspectRatio").GetDouble(),
            root.GetProperty("nearPlane").GetDouble(),
            root.GetProperty("farPlane").GetDouble());

    private static TextLabelGeometry DeserializeTextLabel(JsonElement root) =>
        new(root.GetProperty("text").GetString()!,
            root.GetProperty("fontSize").GetDouble());

    private static CoordinateAxesGeometry DeserializeCoordinateAxes(JsonElement root) =>
        new(root.GetProperty("length").GetDouble());

    private static CoordinateSystemOverlayGeometry DeserializeCoordinateSystemOverlay(JsonElement root) =>
        new(root.GetProperty("length").GetDouble());

    private static MeshGeometry DeserializeMesh(JsonElement root, JsonSerializerOptions options)
    {
        var vertices = JsonSerializer.Deserialize<Point3<double>[]>(root.GetProperty("vertices").GetRawText(), options)!;
        var indices = JsonSerializer.Deserialize<int[]>(root.GetProperty("indices").GetRawText(), options)!;
        return new MeshGeometry(vertices, indices);
    }
}
