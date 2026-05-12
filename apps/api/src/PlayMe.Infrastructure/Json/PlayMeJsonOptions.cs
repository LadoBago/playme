using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayMe.Infrastructure.Json;

/// <summary>
/// Canonical <see cref="JsonSerializerOptions"/> for the whole API surface.
/// Used by the Redis blob, by MVC for HTTP responses, and by SignalR for
/// hub-method payloads — single shape on every wire.
///
/// camelCase property names match the TS client's conventions; enums
/// serialize as lower-case strings ("waitingForOpponent", "host", ...) so
/// the wire stays readable and stable across enum reorderings.
/// </summary>
public static class PlayMeJsonOptions
{
    public static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new RoomCodeJsonConverter());
        options.Converters.Add(new PlayerIdJsonConverter());
        options.Converters.Add(new GameIdJsonConverter());
        options.Converters.Add(new DisplayNameJsonConverter());
        return options;
    }

    public static void ApplyTo(JsonSerializerOptions options)
    {
        var template = CreateDefault();
        options.PropertyNamingPolicy = template.PropertyNamingPolicy;
        options.DictionaryKeyPolicy = template.DictionaryKeyPolicy;
        options.DefaultIgnoreCondition = template.DefaultIgnoreCondition;
        foreach (var converter in template.Converters)
        {
            options.Converters.Add(converter);
        }
    }
}
