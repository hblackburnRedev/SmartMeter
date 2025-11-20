using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;
using System;
using System.IO;
using System.Threading.Tasks;
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
        //ARRANGE
        var testPath = Path.Combine(_tempDir, "sample.txt");
        const string expectedContent = "hello smart meter";

        await File.WriteAllTextAsync(testPath, expectedContent);

        //ACT
        var content = await _service.ReadFileAsync(testPath);

        //ASSERT
        content.Should().Be(expectedContent);
    }

    [Fact]
    public async Task ReadFile_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        // ARRANGE
        var nonExistentPath = Path.Combine(_tempDir, "missing.txt");

        // ACT
        Func<Task> act = () => _service.ReadFileAsync(nonExistentPath);

        // ASSERT
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadFile_WhenIOExceptionOccurs_PropagatesException()
    {
        // ARRANGE
        // Simulate invalid path to trigger an exception on Windows
        var invalidPath = Path.Combine("?:", "invalid", "file.txt");

        // ACT
        Func<Task> act = () => _service.ReadFileAsync(invalidPath);

        // ASSERT
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SaveFile_ThrowsNotImplementedException()
    {
        // ARRANGE
        var path = Path.Combine(_tempDir, "dummy.txt");

        // ACT
        Func<Task> act = () => _service.SaveFileAsync<string>(path);

        // ASSERT
        await act.Should().ThrowAsync<NotImplementedException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
