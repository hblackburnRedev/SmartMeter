using System.Text.Json.Serialization;

namespace SmartMeter.Server.Contracts;

public sealed record NewClientRequest( 
    [property: JsonPropertyName("name")]  string ClientName,
    [property: JsonPropertyName("address")]  string Address);