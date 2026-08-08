using System.Net;
using System.Net.Http.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AspireServiceBus.Sender.Tests;

public class SenderEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SenderEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ServiceBusClient>();
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Send_Returns503_WhenServiceBusClientIsUnavailable()
    {
        var response = await _client.PostAsJsonAsync("/send", new
        {
            timestamp = "2026-08-08T00:00:00Z",
            entityName = "default-queue",
            targetApplication = "receiver",
            bodyJson = "{\"message\":\"hello\"}",
            customHeaders = new Dictionary<string, string> { ["trace"] = "abc" }
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.NotNull(payload);
        Assert.Equal("Service Bus connection is not available yet. The service will stay running and retry once the emulator connection is configured.", payload!["error"]);
    }
}
