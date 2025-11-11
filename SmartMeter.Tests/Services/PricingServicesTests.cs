using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Models;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;
using NSubstitute;
using Xunit;

namespace SmartMeter.Tests.Services;

public class PricingServiceTests
{
    private readonly ILogger<PricingService> _logger = Substitute.For<ILogger<PricingService>>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly IOptions<ReadingConfiguration> _options;

    private readonly string _tempDir;

    public PricingServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pricing-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _options = Options.Create(new ReadingConfiguration { UserReadingsDirectory = _tempDir });
    }

    private PricingService CreateService() =>
        new(_logger, _options, _fileService);

    private static string BuildCsv(params ElectricityRateEntry[] entries)
    {
        using var sw = new StringWriter();
        sw.WriteLine("region,standing_charge_value,standing_charge_unit,unit_rate_value,unit_rate_unit");
        foreach (var e in entries)
        {
            sw.WriteLine($"{e.Region},{e.StandingChargeRate},{e.StandingChargeUnit},{e.UnitChargeRate},{e.UnitChargeUnit}");
        }
        return sw.ToString();
    }

    [Fact]
    public async Task CalculatePrice_ValidRegion_ReturnsExpectedCost()
    {
        var csvContent = BuildCsv(new ElectricityRateEntry
        {
            Region = "London",
            StandingChargeRate = 0.5m,
            StandingChargeUnit = "p/day",
            UnitChargeRate = 0.25m,
            UnitChargeUnit = "£/kWh"
        });

        _fileService.ReadFileAsync(Arg.Any<string>()).Returns(Task.FromResult(csvContent));
        var sut = CreateService();

        var result = await sut.CalculatePriceAsync("London", 100m, "client-001");

        Assert.Equal(25m, result);
        await _fileService.Received(1).ReadFileAsync(Arg.Any<string>());
        _logger.ReceivedWithAnyArgs().Log( LogLevel.Information,0,default!,null,default!);
    }

    [Fact]
    public async Task CalculatePrice_UnknownRegion_ThrowsKeyNotFoundException()
    {
        var csvContent = BuildCsv(new ElectricityRateEntry
        {
            Region = "Scotland",
            StandingChargeRate = 0.4m,
            StandingChargeUnit = "p/day",
            UnitChargeRate = 0.20m,
            UnitChargeUnit = "£/kWh"
        });

        _fileService.ReadFileAsync(Arg.Any<string>()).Returns(Task.FromResult(csvContent));
        var sut = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.CalculatePriceAsync("Wales", 50m, "client-002"));
    }

    [Fact]
    public async Task CalculatePrice_CsvParseFails_ThrowsException()
    {
        _fileService.ReadFileAsync(Arg.Any<string>()).Returns(Task.FromResult("INVALID,CSV,DATA"));
        var sut = CreateService();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CalculatePriceAsync("London", 100m, "client-003"));
    }

    [Fact]
    public async Task CalculatePrice_FileReadFails_ThrowsException()
    {
        _fileService.ReadFileAsync(Arg.Any<string>()).Returns<Task<string>>(_ => throw new IOException("File not found"));
        var sut = CreateService();

        await Assert.ThrowsAsync<IOException>(() =>
            sut.CalculatePriceAsync("London", 100m, "client-004"));
    }

    [Fact]
    public async Task CalculatePrice_CachesBaseRates_AfterFirstCall()
    {
        var csvContent = BuildCsv(new ElectricityRateEntry
        {
            Region = "London",
            StandingChargeRate = 0.5m,
            StandingChargeUnit = "p/day",
            UnitChargeRate = 0.25m,
            UnitChargeUnit = "£/kWh"
        });

        _fileService.ReadFileAsync(Arg.Any<string>()).Returns(Task.FromResult(csvContent));
        var sut = CreateService();

        await sut.CalculatePriceAsync("London", 100m, "client-005");
        await sut.CalculatePriceAsync("London", 200m, "client-006");

        // Cached after first load — should only call ReadFileAsync once
        await _fileService.Received(1).ReadFileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task CalculatePrice_AppendsClientReadingFile()
    {
        var csvContent = BuildCsv(new ElectricityRateEntry
        {
            Region = "London",
            StandingChargeRate = 0.5m,
            StandingChargeUnit = "p/day",
            UnitChargeRate = 0.25m,
            UnitChargeUnit = "£/kWh"
        });

        _fileService.ReadFileAsync(Arg.Any<string>()).Returns(Task.FromResult(csvContent));
        var sut = CreateService();

        var cost = await sut.CalculatePriceAsync("London", 10m, "client-007");

        Assert.True(cost > 0);

        var clientDir = Path.Combine(_tempDir, "client-007");
        Assert.True(Directory.Exists(clientDir));

        var todayFile = Path.Combine(clientDir, $"{DateTime.Now:dd-MM-yyyy}.csv");
        Assert.True(File.Exists(todayFile));
    }
}
