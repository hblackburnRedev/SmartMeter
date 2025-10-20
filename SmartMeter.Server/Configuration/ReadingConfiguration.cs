namespace SmartMeter.Server.Configuration;

public sealed record ReadingConfiguration
{
    public required string UserReadingsDirectory { get; init; }   
}