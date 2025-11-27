using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.UnitTests.Services;

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
    public async Task SaveFileAsync_CreatesFileWithCorrectContent()
    {
        // ARRANGE
        var path = Path.Combine(_tempDir, "sample.txt");
        const string expectedContent = "hello smart meter";

        // ACT
        await _service.SaveFileAsync(expectedContent, path);

        // ASSERT
        File.Exists(path).Should().BeTrue("the file should be created by SaveFileAsync");
        var fileContent = await File.ReadAllTextAsync(path);
        fileContent.Should().Be(expectedContent);
    }

    [Fact]
    public async Task SaveFileAsync_ThenReadFileAsync_ReturnsSameContent()
    {
        // ARRANGE
        var path = Path.Combine(_tempDir, "roundtrip.txt");
        const string content = "round-trip content check";

        // ACT
        await _service.SaveFileAsync(content, path);
        var result = await _service.ReadFileAsync(path);

        // ASSERT
        result.Should().Be(content);
    }

    [Fact]
    public async Task ReadFileAsync_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        // ARRANGE
        var nonExistentPath = Path.Combine(_tempDir, "does-not-exist.txt");

        // ACT
        Func<Task> act = () => _service.ReadFileAsync(nonExistentPath);

        // ASSERT
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
