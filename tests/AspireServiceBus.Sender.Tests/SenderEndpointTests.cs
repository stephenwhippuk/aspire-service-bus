using System.Net;
using System.Net.Http.Json;
using Azure.Messaging.ServiceBus;
using AspireServiceBus.Sender;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AspireServiceBus.Sender.Tests;

public class SenderEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly string _historyFilePath;

    public SenderEndpointTests(WebApplicationFactory<Program> factory)
    {
        _historyFilePath = Path.Combine(Path.GetTempPath(), $"sender-history-{Guid.NewGuid():N}.ndjson");

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["History:FilePath"] = _historyFilePath
                });
            });

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

    [Fact]
    public async Task Send_Failure_IsCapturedInFailedHistory()
    {
        await _client.PostAsJsonAsync("/send", new
        {
            timestamp = "2026-08-08T00:00:00Z",
            entityName = "default-queue",
            targetApplication = "receiver",
            bodyJson = "{\"message\":\"hello\"}",
            customHeaders = new Dictionary<string, string> { ["trace"] = "abc" }
        });

        var failedHistoryResponse = await _client.GetAsync("/history/failed?take=20&skip=0");
        failedHistoryResponse.EnsureSuccessStatusCode();

        var failedHistory = await failedHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();

        Assert.NotNull(failedHistory);
        Assert.NotEmpty(failedHistory!.Items);
        Assert.All(failedHistory.Items, item => Assert.Equal(MessageHistoryOutcome.Failed, item.Outcome));
    }

    [Fact]
    public async Task FailedHistory_DoesNotLeakIntoSuccessHistory()
    {
        await _client.PostAsJsonAsync("/send", new
        {
            timestamp = "2026-08-08T00:00:00Z",
            entityName = "default-queue",
            targetApplication = "receiver",
            bodyJson = "{\"message\":\"hello\"}",
            customHeaders = new Dictionary<string, string> { ["trace"] = "abc" }
        });

        var successHistoryResponse = await _client.GetAsync("/history/success?take=20&skip=0");
        successHistoryResponse.EnsureSuccessStatusCode();

        var successHistory = await successHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();

        Assert.NotNull(successHistory);
        Assert.Empty(successHistory!.Items);
    }

    [Fact]
    public async Task PurgeSucceededHistory_RemovesOnlySuccessEntries()
    {
        await _client.PostAsJsonAsync("/send", new
        {
            timestamp = "2026-08-08T00:00:00Z",
            entityName = "default-queue",
            targetApplication = "receiver",
            bodyJson = "{\"message\":\"hello\"}",
            customHeaders = new Dictionary<string, string> { ["trace"] = "abc" }
        });

        var purgeResponse = await _client.DeleteAsync("/history/success");
        purgeResponse.EnsureSuccessStatusCode();

        var successHistoryResponse = await _client.GetAsync("/history/success?take=20&skip=0");
        successHistoryResponse.EnsureSuccessStatusCode();

        var successHistory = await successHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();
        Assert.NotNull(successHistory);
        Assert.Empty(successHistory!.Items);

        var failedHistoryResponse = await _client.GetAsync("/history/failed?take=20&skip=0");
        failedHistoryResponse.EnsureSuccessStatusCode();

        var failedHistory = await failedHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();
        Assert.NotNull(failedHistory);
        Assert.NotEmpty(failedHistory!.Items);
    }

    [Fact]
    public async Task Resend_WithRepairPayload_CreatesLinkedHistoryEntry()
    {
        await _client.PostAsJsonAsync("/send", new
        {
            timestamp = "2026-08-08T00:00:00Z",
            entityName = "default-queue",
            targetApplication = "receiver",
            bodyJson = "{\"message\":\"hello\"}",
            customHeaders = new Dictionary<string, string> { ["trace"] = "abc" }
        });

        var failedHistoryResponse = await _client.GetAsync("/history/failed?take=20&skip=0");
        failedHistoryResponse.EnsureSuccessStatusCode();

        var failedHistory = await failedHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();
        Assert.NotNull(failedHistory);
        Assert.NotEmpty(failedHistory!.Items);

        var source = failedHistory.Items[0];
        var resendResponse = await _client.PostAsJsonAsync($"/history/{source.Id}/resend", new
        {
            timestamp = "2026-08-09T00:00:00Z",
            entityName = "default-queue",
            targetApplication = "receiver",
            bodyJson = "{\"message\":\"repaired\"}",
            customHeaders = new Dictionary<string, string> { ["trace"] = "repaired" }
        });

        Assert.True(resendResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);

        failedHistoryResponse = await _client.GetAsync("/history/failed?take=20&skip=0");
        failedHistoryResponse.EnsureSuccessStatusCode();

        failedHistory = await failedHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();
        Assert.NotNull(failedHistory);
        Assert.True(failedHistory!.Items.Count >= 2);
        Assert.Contains(failedHistory.Items, item => item.SourceAttemptId == source.Id);
    }
}
