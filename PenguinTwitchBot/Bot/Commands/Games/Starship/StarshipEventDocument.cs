using System.Text.Json;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed record StarshipEventDocument(int SchemaVersion, StarshipEventDefinition Event)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        WriteIndented = true
    };

    public static StarshipEventDefinition Read(string json)
    {
        var document = JsonSerializer.Deserialize<StarshipEventDocument>(json, JsonOptions);
        if (document == null || document.SchemaVersion != 1 || document.Event == null)
            throw new InvalidDataException("Use a supported single-event JSON document (schemaVersion 1).");
        return document.Event;
    }

    public static string Write(StarshipEventDefinition definition) =>
        JsonSerializer.Serialize(new StarshipEventDocument(1, definition), JsonOptions);
}