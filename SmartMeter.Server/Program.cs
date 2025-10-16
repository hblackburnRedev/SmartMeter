using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

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
    }

    private static IServiceProvider CreateServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services
            .Configure<ServerConfiguration>(configuration.GetRequiredSection("ServerConfiguration"));
        
        services
            .AddSingleton<IWebSocketServer, WebSocketServer>()
            .AddSingleton<IFileService, FileService>();

        return services.BuildServiceProvider();
    }
}
