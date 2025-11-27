using SmartMeter.Server.Contracts;
using SmartMeter.Server.Models;

namespace SmartMeter.Server.Services.Abstractions;

public interface IClientService
{
    public Task<SmartMeterClient?> GetSmartMeterClientAsync(Guid clientId);
    
    public Task<SmartMeterClient> AddSmartMeterClientAsync(Guid clientId, string clientName, string  clientAddress);
}