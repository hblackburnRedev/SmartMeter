using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Contracts;

namespace SmartMeter.IntegrationTests;

public sealed class PricingServiceTests : IClassFixture<TestFixture>
{
    private readonly ServerConfiguration _serverConfiguration;
    
    
    public PricingServiceTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        
        _serverConfiguration = new()
        {
            ApiKey = fixture.ApiKey.ToString(),
            IpAddress = fixture.IpAddress,
            Port = fixture.Port,
        };
    }


    [Fact]
    public async Task SaveClientReadingAsync_Should_SaveReadingCorrectly()
    {
        //Arrange
        var buffer = new byte[1024];

        var clientId = Guid.NewGuid().ToString();
        var region = "yorkshire";
        decimal usage = new decimal(1.25);
        
        var request = new ReadingRequest(region, usage);
        var requestAsJson = JsonSerializer.Serialize(request);
        
        using var socket = new ClientWebSocket();
        
        var uri = new Uri($"ws://{_serverConfiguration.IpAddress}:{_serverConfiguration.Port}/ws?apikey={_serverConfiguration.ApiKey}&clientid={clientId}");
        
        await socket.ConnectAsync(uri, CancellationToken.None);
        
        //Act
        string? responseAsString = null;
        
        await socket.SendAsync(
            Encoding.UTF8.GetBytes(requestAsJson),
            WebSocketMessageType.Text,
            WebSocketMessageFlags.EndOfMessage,  
            CancellationToken.None);
        
        var response = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer), 
            CancellationToken.None);
        
        
        responseAsString = Encoding.UTF8.GetString(buffer, 0, response.Count);
        
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, response.CloseStatusDescription, CancellationToken.None);
        
        //Assert
        responseAsString.Should().NotBeNullOrWhiteSpace();

        var readingResponse = JsonSerializer.Deserialize<ReadingResponse>(responseAsString);

        readingResponse.Should().NotBeNull();
        readingResponse.Should().BeOfType<ReadingResponse>();
        
        using (new AssertionScope())
        {
            readingResponse.Region.Should().Be(region);
            readingResponse.Usage.Should().Be(usage);
        }
    }
}