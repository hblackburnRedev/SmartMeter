using System.Text.Json.Serialization;

namespace SmartMeter.Server.Models;

public sealed record ElectricityRate(
    [property: JsonPropertyName("unitrate")] decimal UnitRate,
    [property: JsonPropertyName("standingCharge")] decimal StandingCharge);