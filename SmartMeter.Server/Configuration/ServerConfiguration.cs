using System.Text.Json.Serialization;

namespace SmartMeter.Server.Configuration;

public sealed record ServerConfiguration
{
    public required string ApiKey { get; init; } 

    public required string IpAddress { get; init; }

    public required int Port { get; init; }
}