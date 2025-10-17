using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using SmartMeter.Server.Models;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server.Services;

public class PricingService(IFileService fileService) : IPricingService
{
    private IList<ElectricityRateEntry>? _rates;
    
    private static readonly string _basePricingFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Files", "EnglandElectricityRatesByRegion2025.csv");
    
    private async Task<IList<ElectricityRateEntry>> GetPricingAsync()
    {
        var fileContent = await fileService.ReadFileAsync(_basePricingFilePath);

        using var reader = new StringReader(fileContent);
    
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true
        };

        using var csv = new CsvReader(reader, config);

        var records = csv.GetRecords<ElectricityRateEntry>().ToList();

        return records;
    }

    public async Task<decimal> CalculatePriceAsync(string region, decimal reading)
    {
        _rates ??= await GetPricingAsync();
        
        var entryForRegion = _rates.FirstOrDefault(r =>
            r.Region == region &&
            r.Period == "2025_Q4");

        ArgumentNullException.ThrowIfNull(entryForRegion, nameof(ElectricityRateEntry));
        
        return reading * entryForRegion.UnitChargeRate;
    }
}