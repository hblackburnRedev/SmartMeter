using System.Text.Json.Serialization;

namespace SmartMeter.Server.Configuration;

public sealed record ServerConfiguration
{
    [JsonPropertyName("apikey")]
    public required string ApiKey { get; init; } 

    [JsonPropertyName("ipaddress")]
    public required string IpAddress { get; init; }

    [JsonPropertyName("port")]
    public required int Port { get; init; }
}