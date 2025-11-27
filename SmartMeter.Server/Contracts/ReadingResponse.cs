using System.Text.Json.Serialization;

namespace SmartMeter.Server.Contracts;

public sealed record ReadingResponse
{
    [JsonPropertyName("region")]
    [JsonRequired]
    public required string Region { get; init; }

    [JsonPropertyName("usage")]
    [JsonRequired]
    public required decimal Usage { get; init; }

    [JsonPropertyName("total")]
    [JsonRequired]
    public required decimal Price { get; init; }
}