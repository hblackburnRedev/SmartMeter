namespace SmartMeter.Server.Services.Abstractions;

public interface IFileService
{
    public Task<string> ReadFileAsync(string path);
    
    public Task<T> SaveFileAsync<T>(string path);
}