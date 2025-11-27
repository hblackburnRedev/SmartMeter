using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Models;
using SmartMeter.Server.Services;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.UnitTests.Services;

public sealed class ClientServiceTests : IDisposable
{
    private readonly ILogger<ClientService> _logger = Substitute.For<ILogger<ClientService>>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly IOptions<ReadingConfiguration> _options;

    private readonly string _tempDir;
    private readonly ClientService _sut;

    public ClientServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "client-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _options = Options.Create(new ReadingConfiguration
        {
            UserReadingsDirectory = _tempDir
        });

        _sut = new ClientService(_logger, _options, _fileService);
    }

    [Fact]
    public async Task GetSmartMeterClientAsync_ClientDirectoryMissing_ReturnsNull()
    {
        // ARRANGE
        var clientId = Guid.NewGuid();
        var clientDir = Path.Combine(_tempDir, clientId.ToString());

        Directory.Exists(clientDir).Should().BeFalse();

        // ACT
        var result = await _sut.GetSmartMeterClientAsync(clientId);

        // ASSERT
        result.Should().BeNull();
        await _fileService.DidNotReceive().ReadFileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetSmartMeterClientAsync_ProfileFileMissing_ReturnsNull()
    {
        // ARRANGE
        var clientId = Guid.NewGuid();
        var clientDir = Path.Combine(_tempDir, clientId.ToString());

        Directory.CreateDirectory(clientDir);

        var profilePath = Path.Combine(clientDir, "ClientProfile.json");
        File.Exists(profilePath).Should().BeFalse();

        // ACT
        var result = await _sut.GetSmartMeterClientAsync(clientId);

        // ASSERT
        result.Should().BeNull();
        await _fileService.DidNotReceive().ReadFileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetSmartMeterClientAsync_ProfileExists_ReturnsDeserializedClient()
    {
        // ARRANGE
        var clientId = Guid.NewGuid();
        var clientDir = Path.Combine(_tempDir, clientId.ToString());
        Directory.CreateDirectory(clientDir);

        var profilePath = Path.Combine(clientDir, "ClientProfile.json");

        // Create an empty file so File.Exists(...) returns true
        await File.WriteAllTextAsync(profilePath, string.Empty);

        var expectedClient = new SmartMeterClient(
            clientId,
            "Test Client",
            "123 Test Street");

        var json = JsonSerializer.Serialize(expectedClient);

        _fileService.ReadFileAsync(profilePath).Returns(json);

        // ACT
        var result = await _sut.GetSmartMeterClientAsync(clientId);

        // ASSERT
        using (new AssertionScope())
        {
            result.Should().NotBeNull();
            result.ClientId.Should().Be(expectedClient.ClientId);
            result.ClientName.Should().Be(expectedClient.ClientName);
            result.Address.Should().Be(expectedClient.Address);
            await _fileService.Received(1).ReadFileAsync(profilePath);
        }
    }

    [Fact]
    public async Task AddSmartMeterClientAsync_WhenDirectoryDoesNotExist_CreatesDirectoryAndSavesProfile()
    {
        // ARRANGE
        var clientId = Guid.NewGuid();
        var clientIdString = clientId.ToString();
        var clientDir = Path.Combine(_tempDir, clientIdString);
        var profilePath = Path.Combine(clientDir, "ClientProfile.json");

        Directory.Exists(clientDir).Should().BeFalse();

        // ACT
        var client = await _sut.AddSmartMeterClientAsync(clientId, "New Client", "Some Address");

        // ASSERT
        Directory.Exists(clientDir).Should().BeTrue();

        await _fileService.Received(1)
            .SaveFileAsync(Arg.Any<string>(), profilePath);

        using (new AssertionScope())
        {
            client.ClientId.Should().Be(clientId);
            client.ClientName.Should().Be("New Client");
            client.Address.Should().Be("Some Address");
        }
    }

    [Fact]
    public async Task AddSmartMeterClientAsync_WhenDirectoryAlreadyExists_ThrowsInvalidOperationException()
    {
        // ARRANGE
        var clientId = Guid.NewGuid();
        var clientDir = Path.Combine(_tempDir, clientId.ToString());

        Directory.CreateDirectory(clientDir);

        // ACT
        Func<Task> act = () => _sut.AddSmartMeterClientAsync(clientId, "Existing Client", "Some Address");

        // ASSERT
        await act.Should().ThrowAsync<InvalidOperationException>();

        await _fileService.DidNotReceive()
            .SaveFileAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}