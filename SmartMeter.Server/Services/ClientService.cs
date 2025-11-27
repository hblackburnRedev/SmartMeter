using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Helpers;
using SmartMeter.Server.Models;
using SmartMeter.Server.Services.Abstractions;

namespace SmartMeter.Server.Services;

public sealed class ClientService(
        ILogger<ClientService> logger,
        IOptions<ReadingConfiguration> config,
        IFileService fileService) : IClientService
{
    private const string ClientProfileFileName = "ClientProfile.json";

    public async Task<SmartMeterClient?> GetSmartMeterClientAsync(Guid clientId)
    {
        var clientDir = Path.Combine(config.Value.UserReadingsDirectory, clientId.ToString());
        var profilePath = Path.Combine(clientDir, ClientProfileFileName);

        logger.LogInformation("Fetching SmartMeter client with ID {ClientId}", clientId);

        if (!Directory.Exists(clientDir))
        {
            logger.LogWarning("Client directory does not exist for ClientId {ClientId} at {Path}", clientId, clientDir);
            return null;
        }

        if (!File.Exists(profilePath))
        {
            logger.LogWarning("Client profile file not found for ClientId {ClientId} at {Path}", clientId, profilePath);
            return null;
        }

        var fileContent = await fileService.ReadFileAsync(profilePath);

        logger.LogDebug("Read profile for ClientId {ClientId}: {Json}", clientId, fileContent);

        return JsonDeserializerHelper.TryDeserialize(fileContent, JsonSerializerOptions.Default, out SmartMeterClient? client)
            ? client
            : null;
    }

    public async Task<SmartMeterClient> AddSmartMeterClientAsync(Guid clientId, string clientName, string clientAddress)
    {
        var clientIdAsString = clientId.ToString();
        var clientDirectoryPath = Path.Combine(config.Value.UserReadingsDirectory, clientIdAsString);
        var profilePath = Path.Combine(clientDirectoryPath, ClientProfileFileName);

        logger.LogInformation("Adding new SmartMeter client {ClientId}", clientId);

        if (Directory.Exists(clientDirectoryPath))
        {
            logger.LogError("Cannot create client {ClientId}; directory already exists at {Path}", clientId, clientDirectoryPath);
            throw new InvalidOperationException($"Cannot add client with ClientId: {clientIdAsString} because they already exist");
        }

        Directory.CreateDirectory(clientDirectoryPath);
        logger.LogInformation("Created directory for ClientId {ClientId} at {Path}", clientId, clientDirectoryPath);

        var client = new SmartMeterClient(clientId, clientName, clientAddress);
        var json = JsonSerializer.Serialize(client);

        logger.LogDebug("Writing client profile for ClientId {ClientId} to {Path}", clientId, profilePath);

        await fileService.SaveFileAsync(json, profilePath);

        logger.LogInformation("Client profile saved for ClientId {ClientId}", clientId);

        return client;
    }
}