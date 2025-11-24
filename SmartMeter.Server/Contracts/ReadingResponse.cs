using System.Text.Json.Serialization;

namespace SmartMeter.Server.Contracts;

public sealed record ReadingResponse(
    [property: JsonPropertyName("region")]  string Region,
    [property: JsonPropertyName("usage")]  decimal Usage,
    [property: JsonPropertyName("total")] decimal Price);