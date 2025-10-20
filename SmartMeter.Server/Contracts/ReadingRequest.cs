using System.Text.Json.Serialization;

namespace SmartMeter.Server.Contracts;

public record ReadingRequest(
    [property: JsonPropertyName("region")]  string Region,
    [property: JsonPropertyName("usage")]  decimal Usage );

