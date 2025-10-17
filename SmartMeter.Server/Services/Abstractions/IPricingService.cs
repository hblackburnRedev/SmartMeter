namespace SmartMeter.Server.Services.Abstractions;

public interface IPricingService
{
    public Task<decimal> CalculatePriceAsync(string region, decimal reading);
}