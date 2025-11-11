using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;
using NSubstitute;
using Xunit;

namespace SmartMeter.Tests.Services;

public sealed class FileServiceTests : IDisposable
{
    private readonly ILogger<IFileService> _logger = Substitute.For<ILogger<IFileService>>();
    private readonly FileService _service;
    private readonly string _tempDir;

    public FileServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "file-service-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _service = new FileService(_logger);
    }

    [Fact]
    public async Task ReadFile_FileExists_ReturnsFileContent()
    {
        var testPath = Path.Combine(_tempDir, "sample.txt");
        const string expectedContent = "hello smart meter";
        await File.WriteAllTextAsync(testPath, expectedContent);

        var content = await _service.ReadFileAsync(testPath);

        Assert.Equal(expectedContent, content);
    }

    [Fact]
    public async Task ReadFile_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(_tempDir, "missing.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.ReadFileAsync(nonExistentPath));
    }

    [Fact]
    public async Task ReadFile_WhenIOExceptionOccurs_PropagatesException()
    {
        // simulate an invalid path to trigger IOException
        var invalidPath = Path.Combine("?:", "invalidpath.txt");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _service.ReadFileAsync(invalidPath));
    }

    [Fact]
    public async Task SaveFile_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            _service.SaveFileAsync<string>("dummy.txt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
