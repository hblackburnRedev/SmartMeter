using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.IntegrationTests;

public class TestFixture : IAsyncLifetime
{
    public Guid ApiKey = Guid.NewGuid();
    public int Port = 8080;
    public string IpAddress = "127.0.0.1";
    public DirectoryInfo ReadingsDirectory = Directory.CreateTempSubdirectory();
    
    private IHost? _host;

    public async Task InitializeAsync()
    {
        // Create a host similar to Program.cs
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServerConfiguration:Port"] = Port.ToString(),
            ["ServerConfiguration:ApiKey"] = ApiKey.ToString(),
            ["ServerConfiguration:IpAddress"] = IpAddress,
            ["ReadingConfiguration:UserReadingsDirectory"] = ReadingsDirectory.FullName,
        });

        builder.Services.AddLogging();

        builder.Services
            .Configure<ServerConfiguration>(builder.Configuration.GetSection("ServerConfiguration"))
            .Configure<ReadingConfiguration>(builder.Configuration.GetSection("ReadingConfiguration"));

        builder.Services
            .AddSingleton<IFileService, FileService>()
            .AddSingleton<IPricingService, PricingService>();

        builder.Services.AddHostedService<WebSocketServer>();
        
        var host = builder.Build();

        await host.RunAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}