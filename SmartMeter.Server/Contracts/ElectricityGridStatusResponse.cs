using System.Text.Json.Serialization;

namespace SmartMeter.Server.Contracts;

public sealed record ElectricityGridStatusResponse{
    
    [JsonPropertyName("status")]
    [JsonRequired]
    public required string Status { get; init; }
}