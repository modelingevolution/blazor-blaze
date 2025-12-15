using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorBlaze.Server;

/// <summary>
/// Recording metadata from JSON index file.
/// </summary>
public record RecordingMetadata
{
    public string Caps { get; init; } = string.Empty;

    [JsonConverter(typeof(Iso8601DateTimeConverter))]
    public DateTime Started { get; set; }

    [JsonConverter(typeof(Iso8601DateTimeConverter))]
    public DateTime Triggered { get; set; }

    public FramesIndex Index { get; init; } = new();

    /// <summary>
    /// Get absolute DateTime for a frame based on its relative timestamp.
    /// </summary>
    public DateTime GetFrameAbsoluteTime(FrameIndex frame) => frame.GetAbsoluteTime(Started);
}

/// <summary>
/// Frame index entry with byte offset, size, and timestamp.
/// </summary>
public record FrameIndex
{
    /// <summary>Byte offset in the MJPEG file.</summary>
    [JsonPropertyName("s")]
    public ulong Start { get; init; }

    /// <summary>Frame size in bytes.</summary>
    [JsonPropertyName("sz")]
    public ulong Size { get; init; }

    /// <summary>Relative timestamp in microseconds from recording start.</summary>
    [JsonPropertyName("t")]
    public ulong RelativeTimestampMicroseconds { get; init; }

    /// <summary>Get absolute DateTime based on recording start time.</summary>
    public DateTime GetAbsoluteTime(DateTime recordingStarted)
        => recordingStarted.Add(TimeSpan.FromMicroseconds(RelativeTimestampMicroseconds));
}

/// <summary>
/// Sorted dictionary of frame sequence numbers to frame index entries.
/// </summary>
public class FramesIndex : SortedList<ulong, FrameIndex>
{
}

/// <summary>
/// ISO 8601 DateTime JSON converter.
/// </summary>
public class Iso8601DateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();
        if (string.IsNullOrEmpty(dateString))
            return default;

        return DateTime.Parse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O"));
    }
}
