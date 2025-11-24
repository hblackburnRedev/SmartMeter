using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
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
        decimal pricing = new decimal(1.25);
        
        var request = new ReadingRequest(region, pricing);
        
        using var socket = new ClientWebSocket();
        
        await socket.ConnectAsync(new Uri($"{_serverConfiguration.IpAddress}:{_serverConfiguration.Port}?apikey={_serverConfiguration.ApiKey}?clientid={clientId}"), CancellationToken.None);
        //Act
        string? responseAsString = null;
        
        while (socket.State is WebSocketState.Open)
        {
            await socket.SendAsync(
                Encoding.UTF8.GetBytes(request.ToString()),
                WebSocketMessageType.Text,
                WebSocketMessageFlags.EndOfMessage,  
                CancellationToken.None);
            
            var response = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), 
                CancellationToken.None);
            
            
            responseAsString = Encoding.UTF8.GetString(buffer, 0, response.Count);
            
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, response.CloseStatusDescription, CancellationToken.None);
        }
        //Assert

        responseAsString.Should().NotBeNullOrWhiteSpace();
    }
}