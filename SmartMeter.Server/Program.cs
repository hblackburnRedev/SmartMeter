using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server;

public class Program
{
    private static readonly CancellationTokenSource CancellationTokenSource = new();
    
    public static async Task Main(string[] args)
    {
        
        var builder = Host.CreateApplicationBuilder(args);
        
        builder.Services.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Information);
        });

        builder.Services
            .Configure<ServerConfiguration>(builder.Configuration.GetRequiredSection("ServerConfiguration"))
            .Configure<ReadingConfiguration>(builder.Configuration.GetRequiredSection("ReadingConfiguration"));
        
        builder.Services
            .AddSingleton<IFileService, FileService>()
            .AddSingleton<IPricingService, PricingService>();

        builder.Services
            .AddHostedService<WebSocketServer>();
        
        var host = builder.Build();
        
        try
        {
            await host.RunAsync(CancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }
}