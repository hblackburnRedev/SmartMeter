using System.Text.Json.Serialization;

namespace SmartMeter.Server.Models;

public record SmartMeterClient(
    [property: JsonPropertyName("clientId")]  Guid ClientId,
    [property: JsonPropertyName("name")]  string ClientName,
    [property: JsonPropertyName("address")]  string Address);