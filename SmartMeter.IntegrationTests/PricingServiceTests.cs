using FluentAssertions;
using FluentAssertions.Execution;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.IntegrationTests;

public sealed class PricingServiceTests : IClassFixture<TestFixture>
{
    private readonly IPricingService _pricingService;
    private readonly string _userReadingsDir;
    
    public PricingServiceTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _pricingService = fixture.PricingService;
        _userReadingsDir = fixture.UserReadingsDir;
    }


    [Fact]
    public async Task SaveClientReadingAsync_Should_SaveReadingCorrectly()
    {
        //Arrange
        
        var clientId = Guid.NewGuid().ToString();
        var region = "yorkshire";
        decimal pricing = new decimal(1.25);
        
        //Act
        
        await _pricingService.CalculatePriceAsync(region, pricing, clientId);
        
        var readings = await _pricingService.GetClientReadingsForDateAsync(clientId, DateTime.Now.Date);
        
        //Assert
        readings.Should().HaveCount(1);
        
        using (new AssertionScope())
        {
            var reading = readings.First();
            reading.EntryDateTime.Date.Should().Be(DateTime.Now.Date);
            reading.Reading.Should().Be(pricing);
            reading.Total.Should().Be(reading.Price * pricing);
        }
    }
}