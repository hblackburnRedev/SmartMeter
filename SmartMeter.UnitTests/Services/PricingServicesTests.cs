using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Models;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.UnitTests.Services;

public class PricingServiceTests
{
    private readonly ILogger<PricingService> _logger = Substitute.For<ILogger<PricingService>>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly IOptions<ReadingConfiguration> _options;

    private readonly string _tempDir;
    private readonly PricingService _sut;


    public PricingServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pricing-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _options = Options.Create(new ReadingConfiguration { UserReadingsDirectory = _tempDir });

        _sut = new PricingService(_logger, _options, _fileService);
    }

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
        //ARRANGE
        var csvContent = BuildCsv(new ElectricityRateEntry
        {
            Region = "London",
            StandingChargeRate = 0.5m,
            StandingChargeUnit = "p/day",
            UnitChargeRate = 0.25m,
            UnitChargeUnit = "£/kWh"
        });

        _fileService.ReadFileAsync(Arg.Any<string>()).Returns(Task.FromResult(csvContent));

        //ACT
        var result = await _sut.CalculatePriceAsync("London", 100m, "client-001");

        //ASSERT
        result.Should().Be(25m);

        await _fileService.Received(1)
            .ReadFileAsync(Arg.Any<string>());

        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CalculatePrice_UnknownRegion_ThrowsKeyNotFoundException()
    {
        // ARRANGE
        var csv = BuildCsv(new ElectricityRateEntry
        {
            Region = "Scotland",
            StandingChargeRate = 0.4m,
            StandingChargeUnit = "p/day",
            UnitChargeRate = 0.20m,
            UnitChargeUnit = "£/kWh"
        });

        _fileService.ReadFileAsync(Arg.Any<string>())
            .Returns(csv);

        // ACT
        var act = () => _sut.CalculatePriceAsync("Wales", 50m, "client-002");

        // ASSERT
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CalculatePrice_CachesBaseRates_AfterFirstCall()
    {
        // ARRANGE
        var csv = BuildCsv(new ElectricityRateEntry
        {
            Region = "London",
            StandingChargeRate = 0.5m,
            StandingChargeUnit = "p/day",
            UnitChargeRate = 0.25m,
            UnitChargeUnit = "£/kWh"
        });

        _fileService.ReadFileAsync(Arg.Any<string>())
            .Returns(csv);

        // ACT
        await _sut.CalculatePriceAsync("London", 100m, "client-005");
        await _sut.CalculatePriceAsync("London", 200m, "client-006");

        // ASSERT
        await _fileService.Received(1).ReadFileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task CalculatePrice_AppendsClientReadingFile()
    {
        // ARRANGE
        var csv = BuildCsv(new ElectricityRateEntry
        {
            Region = "London",
            StandingChargeRate = 0.5m,
            StandingChargeUnit = "p/day",
            UnitChargeRate = 0.25m,
            UnitChargeUnit = "£/kWh"
        });

        _fileService.ReadFileAsync(Arg.Any<string>())
            .Returns(csv);

        // ACT
        var cost = await _sut.CalculatePriceAsync("London", 10m, "client-007");

        // ASSERT
        cost.Should().BeGreaterThan(0);

        var clientDir = Path.Combine(_tempDir, "client-007");
        Directory.Exists(clientDir).Should().BeTrue();

        var today = DateTime.Today.ToString("dd-MM-yyyy");
        var todayFile = Path.Combine(clientDir, $"{today}.csv");
        File.Exists(todayFile).Should().BeTrue();
    }
}
