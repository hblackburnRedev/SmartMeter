using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using SmartMeter.Server.Configuration;
using Xunit;
using SmartMeter.Server;
using NSubstitute;

namespace SmartMeter.Server.Tests;
public class ConfigurationBindingTests
{
    [Fact]
    public void ServerConfiguration_BindsFromJson()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["ServerConfiguration:ApiKey"] = "secret",
            ["ServerConfiguration:IpAddress"] = "127.0.0.1",
            ["ServerConfiguration:Port"] = "9000"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var serverConfig = config.GetRequiredSection("ServerConfiguration").Get<ServerConfiguration>();

        Assert.NotNull(serverConfig);
        Assert.Equal("secret", serverConfig.ApiKey);
        Assert.Equal("127.0.0.1", serverConfig.IpAddress);
        Assert.Equal(9000, serverConfig.Port);
    }

    [Fact]
    public void ReadingConfiguration_BindsFromJson()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["ReadingConfiguration:UserReadingsDirectory"] = "/tmp/readings"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var readingConfig = config.GetRequiredSection("ReadingConfiguration").Get<ReadingConfiguration>();

        Assert.NotNull(readingConfig);
        Assert.Equal("/tmp/readings", readingConfig.UserReadingsDirectory);
    }
    [Fact]
    public void ServerConfiguration_MissingRequiredFields()
    {
        var inMemory = new Dictionary<string, string?>
        {
            // No ApiKey
            ["ServerConfiguration:IpAddress"] = "127.0.0.1",
            ["ServerConfiguration:Port"] = "9000"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        Assert.ThrowsAny<Exception>(() =>
        {
            _ = config.GetRequiredSection("ServerConfiguration").Get<ServerConfiguration>();
        });
    }

    [Fact]
    public void ReadingConfiguration_MissingRequiredFields()
    {
        var inMemory = new Dictionary<string, string?>
        {
            // No UserReadingsDirectory
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        Assert.ThrowsAny<Exception>(() =>
        {
            _ = config.GetRequiredSection("ReadingConfiguration").Get<ReadingConfiguration>();
        });
    }
}