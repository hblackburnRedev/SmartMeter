using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Models;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server.Services;

public class PricingService(ILogger<PricingService> logger, IOptions<ReadingConfiguration> config, IFileService fileService) : IPricingService
{
    private IList<ElectricityRateEntry>? _baseRates;
    
    private static readonly string BasePricingFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Files", "EnglandElectricityRatesByRegion2025.csv");
    
    private async Task<IList<T>> GetCsvRecordsAsync<T>(string path)
    {
        try
        {
            logger.LogDebug("Reading CSV file: {Path}", path);

            var fileContent = await fileService.ReadFileAsync(path);
            using var reader = new StringReader(fileContent);

            var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true
            };

            using var csv = new CsvReader(reader, csvConfiguration);
            var records = csv.GetRecords<T>().ToList();

            logger.LogInformation("Loaded {Count} records from {Path}", records.Count, path);
            return records;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed reading/parsing CSV: {Path}", path);
            throw;
        }
    }

    private async Task SaveClientReadingAsync(decimal reading, decimal price, decimal cost, string clientId)
    {
        var clientReadingsPath = Path.Combine(config.Value.UserReadingsDirectory, clientId);
        
        var currentReadingFileForClient = Path.Combine(
            clientReadingsPath,
            $"{DateTime.Now:dd-MM-yyyy}.csv"
        );

        try
        {
            var newEntry = new ClientReadingEntry
            {
                Reading = reading,
                Price = price,
                Total = cost,
                EntryDateTime = DateTime.Now
            };

            // Configure CsvHelper to append and skip headers
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false, // important: no headers
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true
            };

            // Open file for append — create if not exists
            await using var stream = new FileStream(currentReadingFileForClient, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream);

            await using var csv = new CsvWriter(writer, csvConfig);
            csv.WriteRecord(newEntry);
            await csv.NextRecordAsync();

            logger.LogInformation(
                "Appended reading for client {ClientID}: Reading={Reading}, Price={Price}, Total={Total}",
                clientId, reading, price, cost);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving client reading for client {ClientID}", clientId);
            throw;
        }
    }

    public async Task<decimal> CalculatePriceAsync(
        string region,
        decimal reading,
        string clientId)
    {
        try
        {
            logger.LogInformation("Calculating price for client {ClientID}: Region={Region}, Reading={Reading}",
                clientId, region, reading);

            // Load base pricing once
            if (_baseRates is null)
            {
                logger.LogInformation("Loading base rates from {Path}", BasePricingFilePath);
                _baseRates = await GetCsvRecordsAsync<ElectricityRateEntry>(BasePricingFilePath);
                logger.LogInformation("Base rate rows loaded: {Count}", _baseRates.Count);
            }

            var entryForRegion = _baseRates
                .FirstOrDefault(r => r.Region.Equals(region, StringComparison.OrdinalIgnoreCase));

            if (entryForRegion is null)
            {
                logger.LogWarning("No pricing entry found for Region={Region}", region);
                throw new KeyNotFoundException($"Region '{region}' not found in pricing data.");
            }

            // If UnitChargeRate is in pence/kWh, convert to £ by dividing by 100m; remove if you want pence result.
            var cost = reading * entryForRegion.UnitChargeRate;

            logger.LogInformation(
                "Computed cost for {ClientID}: Region={Region}, Reading={Reading}, UnitRate={Rate}, Cost={Cost}",
                clientId, region, reading, entryForRegion.UnitChargeRate, cost);

            await SaveClientReadingAsync(reading, entryForRegion.UnitChargeRate, cost, clientId);

            return cost;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error during price calculation. ClientID={ClientID}, Region={Region}, Reading={Reading}",
                clientId, region, reading);
            throw;
        }
    }
    
    public async Task<IList<ClientReadingEntry>> GetClientReadingsForDateAsync(string clientId, DateTime date)
    {
        var clientReadingsPath = Path.Combine(config.Value.UserReadingsDirectory, clientId);
        var filePath = Path.Combine(clientReadingsPath, $"{date:dd-MM-yyyy}.csv");

        try
        {
            if (!File.Exists(filePath))
            {
                logger.LogInformation("No readings file found for client {ClientID} on {Date}", clientId, date.ToString("dd-MM-yyyy"));
                return new List<ClientReadingEntry>();
            }

            logger.LogDebug("Reading client readings from {Path}", filePath);

            var fileContent = await fileService.ReadFileAsync(filePath);
            using var reader = new StringReader(fileContent);

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,       // we wrote these without headers
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true
            };
            
            using var csv = new CsvReader(reader, csvConfig);
            var records = csv.GetRecords<ClientReadingEntry>().ToList();

            logger.LogInformation("Loaded {Count} readings for client {ClientID} on {Date}",
                records.Count, clientId, date.ToString("dd-MM-yyyy"));

            return records;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read client readings for client {ClientID} on {Date}",
                clientId, date.ToString("dd-MM-yyyy"));
            throw;
        }
    }
}