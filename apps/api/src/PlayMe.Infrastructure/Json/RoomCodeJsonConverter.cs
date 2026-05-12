using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Json;

/// <summary>
/// Serializes <see cref="RoomCode"/> as a JSON string ("ABCxyz...") instead
/// of the record-struct default ({"value": "ABCxyz..."}). Registered in
/// <see cref="PlayMeJsonOptions"/> so the Redis blob and the HTTP response
/// share one shape.
/// </summary>
public sealed class RoomCodeJsonConverter : JsonConverter<RoomCode>
{
    public override RoomCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()
            ?? throw new JsonException("RoomCode must be a non-null string.");
        return new RoomCode(value);
    }

    public override void Write(Utf8JsonWriter writer, RoomCode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
