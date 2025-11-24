using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using SmartMeter.Server.Configuration;
using SmartMeter.Server.Contracts;

namespace SmartMeter.IntegrationTests;

public sealed class SmartMeterTests : IClassFixture<TestFixture>
{
    private readonly ServerConfiguration _serverConfiguration;
    
    
    public SmartMeterTests(TestFixture fixture)
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
    public async Task SmartMeter_Should_Return_Correct_ReadingConfiguration_When_Provided_Valid_Request()
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
    
    [Fact]
    public async Task SmartMeter_Should_Return_Error_And_Close_Connection_When_Provided_Invalid_Region()
    {
        //Arrange
        var buffer = new byte[1024];

        var clientId = Guid.NewGuid().ToString();
        var region = "invalid";
        decimal usage = new decimal(1.25);
        
        var request = new ReadingRequest(region, usage);
        var requestAsJson = JsonSerializer.Serialize(request);
        
        using var socket = new ClientWebSocket();
        
        var uri = new Uri($"ws://{_serverConfiguration.IpAddress}:{_serverConfiguration.Port}/ws?apikey={_serverConfiguration.ApiKey}&clientid={clientId}");
        
        await socket.ConnectAsync(uri, CancellationToken.None);
        
        //Act
        await socket.SendAsync(
            Encoding.UTF8.GetBytes(requestAsJson),
            WebSocketMessageType.Text,
            WebSocketMessageFlags.EndOfMessage,  
            CancellationToken.None);
        
        var response = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer), 
            CancellationToken.None);
        
        
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, response.CloseStatusDescription, CancellationToken.None);
        
        //Assert
        using (new AssertionScope())
        {
            response.MessageType.Should().Be(WebSocketMessageType.Close);
            response.CloseStatus.Should().Be(WebSocketCloseStatus.InternalServerError);
        }
    }
    
    [Fact]
    public async Task SmartMeter_Should_Close_With_InvalidPayloadData_When_Message_Is_Not_Valid_ReadingRequest()
    {
        // Arrange
        var buffer = new byte[1024];

        var clientId = Guid.NewGuid().ToString();
        using var socket = new ClientWebSocket();

        var uri = new Uri(
            $"ws://{_serverConfiguration.IpAddress}:{_serverConfiguration.Port}/ws?apikey={_serverConfiguration.ApiKey}&clientid={clientId}");

        await socket.ConnectAsync(uri, CancellationToken.None);

        // Act
        var invalidPayload = "invalid";
        
        await socket.SendAsync(
            Encoding.UTF8.GetBytes(invalidPayload),
            WebSocketMessageType.Text,
            WebSocketMessageFlags.EndOfMessage,
            CancellationToken.None);

        var response = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer),
            CancellationToken.None);

        // Assert'
        using (new AssertionScope())
        {
            response.MessageType.Should().Be(WebSocketMessageType.Close);
            response.CloseStatus.Should().Be(WebSocketCloseStatus.InvalidPayloadData);
        }
    }

    [Fact]
    public async Task SmartMeter_Should_Handle_Multiple_Readings_On_Same_WebSocket_Connection()
    {
        // Arrange
        var buffer = new byte[1024];

        var clientId = Guid.NewGuid().ToString();
        var region = "yorkshire";
        decimal[] usages = [1.25m, 2.5m, 10m];

        using var socket = new ClientWebSocket();

        var uri = new Uri(
            $"ws://{_serverConfiguration.IpAddress}:{_serverConfiguration.Port}/ws?apikey={_serverConfiguration.ApiKey}&clientid={clientId}");

        await socket.ConnectAsync(uri, CancellationToken.None);

        // Act & Assert
        foreach (var usage in usages)
        {
            var request = new ReadingRequest(region, usage);
            var requestAsJson = JsonSerializer.Serialize(request);

            await socket.SendAsync(
                Encoding.UTF8.GetBytes(requestAsJson),
                WebSocketMessageType.Text,
                WebSocketMessageFlags.EndOfMessage,
                CancellationToken.None);

            var response = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);

            var responseAsString = Encoding.UTF8.GetString(buffer, 0, response.Count);

            responseAsString.Should().NotBeNullOrWhiteSpace();

            var readingResponse = JsonSerializer.Deserialize<ReadingResponse>(responseAsString);

            readingResponse.Should().NotBeNull();
            readingResponse.Should().BeOfType<ReadingResponse>();

            using (new AssertionScope())
            {
                readingResponse!.Region.Should().Be(region);
                readingResponse.Usage.Should().Be(usage);
            }
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client finished sending readings", CancellationToken.None);
    }

    [Fact]
    public async Task SmartMeter_Should_Reject_NonWebSocket_Request_With_BadRequest()
    {
        // Arrange
        using var httpClient = new HttpClient();
        
        var clientId = Guid.NewGuid().ToString();
        
        var uri = new Uri(
            $"ws://{_serverConfiguration.IpAddress}:{_serverConfiguration.Port}/ws?apikey={_serverConfiguration.ApiKey}&clientid={clientId}");

        // Act
        var response = await httpClient.GetAsync(uri);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            body.Should().Contain("only supports WebSocket connections");
        }
    }

    [Fact]
    public async Task SmartMeter_Should_Return_Unauthorized_When_Missing_Credentials()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var uri = new Uri($"http://{_serverConfiguration.IpAddress}:{_serverConfiguration.Port}/ws");

        // Act
        var response = await httpClient.GetAsync(uri);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            body.Should().Contain("Unauthorized: invalid credentials.");
        }
    }

    [Fact]
    public async Task SmartMeter_Should_Return_Unauthorized_When_ApiKey_Is_Invalid()
    {
        // Arrange
        using var httpClient = new HttpClient();

        var invalidApiKey = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid().ToString();

        var uri = new Uri(
            $"http://{_serverConfiguration.IpAddress}:{_serverConfiguration.Port}/ws?apikey={invalidApiKey}&clientid={clientId}");

        // Act
        var response = await httpClient.GetAsync(uri);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            body.Should().Contain("Unauthorized: invalid API key.");
        }
    }
}