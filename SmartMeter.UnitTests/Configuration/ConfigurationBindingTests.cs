using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SmartMeter.Server.Configuration;

namespace SmartMeter.UnitTests.Configuration;
public class ConfigurationBindingTests
{
    [Fact]
    public void ServerConfiguration_BindsFromJson()
    {
        //ARRANGE
        var inMemory = new Dictionary<string, string?>
        {
            ["ServerConfiguration:ApiKey"] = "secret",
            ["ServerConfiguration:IpAddress"] = "127.0.0.1",
            ["ServerConfiguration:Port"] = "9000"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        //ACT
        var serverConfig = config
            .GetRequiredSection("ServerConfiguration")
            .Get<ServerConfiguration>();

        // ASSERT
        serverConfig.Should().NotBeNull();
        serverConfig.Should().Be(new ServerConfiguration
        {
            ApiKey = "secret",
            IpAddress = "127.0.0.1",
            Port = 9000
        });
    }

    [Fact]
    public void ReadingConfiguration_BindsFromJson()
    {

        //ARRANGE
        var inMemory = new Dictionary<string, string?>
        {
            ["ReadingConfiguration:UserReadingsDirectory"] = "/tmp/readings"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        //ACT
        var readingConfig = config.GetRequiredSection("ReadingConfiguration").Get<ReadingConfiguration>();

        //ASSERT
        readingConfig.Should().NotBeNull();

        readingConfig.Should().Be(new ReadingConfiguration
        {
            UserReadingsDirectory = "/tmp/readings"
        });
    }

    [Fact]
    public void ReadingConfiguration_MissingRequiredFields()
    {
        //ARRANGE
        var inMemory = new Dictionary<string, string?>
        {
            // No UserReadingsDirectory
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        // ACT + ASSERT
        FluentActions
            .Invoking(() => config
                .GetRequiredSection("ReadingConfiguration")
                .Get<ReadingConfiguration>())
            .Should()
            .Throw<InvalidOperationException>();
    }
}