using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.IntegrationTests;

public class TestFixture : IAsyncLifetime
{
    public IPricingService PricingService = null!;
    public string UserReadingsDir = string.Empty;
    
    public Task InitializeAsync()
    {
        var temp = Directory.CreateTempSubdirectory("test_readings");
        
        UserReadingsDir = temp.FullName;
        
        var readingConfig = new ReadingConfiguration()
        {
             UserReadingsDirectory = UserReadingsDir,
        };
        var loggingFactory = LoggerFactory.Create(x => x.ClearProviders());

        var fileService = new FileService(new Logger<FileService>(loggingFactory));
        
        PricingService = new PricingService(
            new Logger<PricingService>(loggingFactory),
            new OptionsWrapper<ReadingConfiguration>(readingConfig),
            fileService);
        
        return Task.CompletedTask;
        
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}