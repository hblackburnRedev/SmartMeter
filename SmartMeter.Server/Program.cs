using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        var config = builder.Build();

        var services = CreateServices(config);
        
        var server = services.GetRequiredService<IWebSocketServer>();
        
        try
        {
            await server.StartServer();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
        //test change
    }

    private static IServiceProvider CreateServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services
            .Configure<ServerConfiguration>(configuration.GetRequiredSection("ServerConfiguration"))
            .Configure<ReadingConfiguration>(configuration.GetRequiredSection("ReadingConfiguration"));
        
        services
            .AddSingleton<IWebSocketServer, WebSocketServer>()
            .AddSingleton<IFileService, FileService>()
            .AddSingleton<IPricingService, PricingService>();

        return services.BuildServiceProvider();
    }
}
