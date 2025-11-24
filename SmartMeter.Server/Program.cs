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
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Logging
                services.AddLogging(b =>
                {
                    b.AddConsole();
                    b.SetMinimumLevel(LogLevel.Information);
                });

                // Configuration binding
                services
                    .Configure<ServerConfiguration>(context.Configuration.GetRequiredSection("ServerConfiguration"))
                    .Configure<ReadingConfiguration>(context.Configuration.GetRequiredSection("ReadingConfiguration"));

                // Services
                services
                    .AddSingleton<IFileService, FileService>()
                    .AddSingleton<IPricingService, PricingService>();

                // Hosted services
                services.AddHostedService<WebSocketServer>();
            })
            .Build();
        
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