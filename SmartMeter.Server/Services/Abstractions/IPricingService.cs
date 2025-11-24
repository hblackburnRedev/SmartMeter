using SmartMeter.Server.Models;

namespace SmartMeter.Server.Services.Abstractions;

public interface IPricingService
{
    public Task<decimal> CalculatePriceAsync(
        string region,
        decimal reading,
        string clientId);

    public Task<IList<ClientReadingEntry>> GetClientReadingsForDateAsync(
        string clientId,
        DateTime date);
}