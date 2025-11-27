using System.Text.Json.Serialization;

public sealed record NewClientRequest
{
    [JsonPropertyName("name")]
    [JsonRequired]
    public required string ClientName { get; init; }

    [JsonPropertyName("address")]
    [JsonRequired]
    public required string Address { get; init; }
}