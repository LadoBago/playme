using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Json;

public sealed class DisplayNameJsonConverter : JsonConverter<DisplayName>
{
    public override DisplayName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()
            ?? throw new JsonException("DisplayName must be a non-null string.");
        return DisplayName.Create(value);
    }

    public override void Write(Utf8JsonWriter writer, DisplayName value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
