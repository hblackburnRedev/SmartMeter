using Microsoft.Extensions.Logging;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server.Services;

public sealed class FileService(ILogger<IFileService> logger) : IFileService
{
    public async Task<string> ReadFileAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var fileContent = await File.ReadAllTextAsync(path);

            return fileContent;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
    }

    public Task<T> SaveFileAsync<T>(string path)
    {
        throw new NotImplementedException();
    }
}