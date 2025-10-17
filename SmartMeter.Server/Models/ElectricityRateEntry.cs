using CsvHelper.Configuration.Attributes;

namespace SmartMeter.Server.Models;

public sealed record ElectricityRateEntry
{
    [Name("region")]
    public required string Region { get; init; }

    [Name("period")]
    public required string Period { get; init; }

    [Name("standing_charge_value")]
    public required decimal StandingChargeRate { get; init; }

    [Name("standing_charge_unit")]
    public required string StandingChargeUnit { get; init; } 
    
    [Name("unit_rate_value")]
    public required decimal UnitChargeRate { get; init; }

    [Name("unit_rate_unit")]
    public required string UnitChargeUnit { get; init; }
}