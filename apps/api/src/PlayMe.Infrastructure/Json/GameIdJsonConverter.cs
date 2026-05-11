using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Json;

public sealed class GameIdJsonConverter : JsonConverter<GameId>
{
    public override GameId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()
            ?? throw new JsonException("GameId must be a non-null string.");
        return new GameId(value);
    }

    public override void Write(Utf8JsonWriter writer, GameId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
