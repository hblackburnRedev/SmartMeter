using Microsoft.Extensions.Logging;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server.Services;

public sealed class FileService(ILogger<IFileService> logger) : IFileService
{
    public async Task<string> ReadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            logger.LogError("File {Path} does not exist", path);
            throw new FileNotFoundException(path);
        }

        var fileContent = await File.ReadAllTextAsync(path);

        return fileContent;
    }

    public Task SaveFileAsync(string content, string path)
    {
        return File.WriteAllTextAsync(path, content);
    }
}