namespace SmartMeter.Server.Models;

public record ClientReadingEntry
{
    public required decimal Reading { get; init; }
    
    public required decimal Price { get; init; }
    
    public required decimal Total { get; init; } 
    
    public required DateTime EntryDateTime  { get; init; }
}