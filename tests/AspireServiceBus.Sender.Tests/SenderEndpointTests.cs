using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AspireServiceBus.Sender;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireServiceBus.Sender.Tests;

public class SenderEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _historyFilePath;

    public SenderEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
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
    public void ResolveConnectionString_PrefersEnvironmentValueWhenConfiguredEntryIsEmpty()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:servicebus"] = string.Empty,
                ["ConnectionStrings__servicebus"] = "Endpoint=sb://127.0.0.1:32836;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
            })
            .Build();

        var connectionString = ServiceBusConnectionSettings.ResolveConnectionString(configuration);

        Assert.Equal("Endpoint=sb://127.0.0.1:32836;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;", connectionString);
    }

    [Fact]
    public void CreateDiagnostics_ExposesResolvedAndRawSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceBus:QueueName"] = "priority-queue",
                ["ConnectionStrings:servicebus"] = string.Empty,
                ["ConnectionStrings__servicebus"] = "Endpoint=sb://127.0.0.1:32836;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                ["History:FilePath"] = "/tmp/history.ndjson",
                ["Logging:LocalFilePath"] = "/tmp/sender.log"
            })
            .Build();

        var diagnostics = ServiceBusConnectionSettings.CreateDiagnostics(configuration, "priority-queue", "/tmp/history.ndjson", "/tmp/sender.log");

        Assert.Equal("priority-queue", diagnostics.QueueName);
        Assert.True(diagnostics.HasResolvedConnectionString);
        Assert.Equal("Endpoint=sb://127.0.0.1:32836;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;", diagnostics.ResolvedConnectionString);
        Assert.Equal("/tmp/history.ndjson", diagnostics.HistoryFilePath);
        Assert.Equal("/tmp/sender.log", diagnostics.LocalLogPath);
    }

    [Fact]
    public void Composer_UsesSingleEntityNameField()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AspireServiceBus.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var htmlPath = Path.Combine(directory!.FullName, "src", "AspireServiceBus.Sender", "wwwroot", "index.html");
        Assert.True(File.Exists(htmlPath), $"Expected composer markup at {htmlPath}");

        var html = File.ReadAllText(htmlPath);
        Assert.Contains("<input id=\"entityName\"", html);
        Assert.DoesNotContain("id=\"entityType\"", html);
        Assert.Contains("entity-name", html);
    }

    [Fact]
    public void Explorer_PageRefreshesEntityCountsPeriodically()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AspireServiceBus.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var htmlPath = Path.Combine(directory!.FullName, "src", "AspireServiceBus.Sender", "wwwroot", "index.html");
        Assert.True(File.Exists(htmlPath), $"Expected composer markup at {htmlPath}");

        var html = File.ReadAllText(htmlPath);
        Assert.Contains("window.setInterval", html);
        Assert.Contains("loadExplorerEntities().catch", html);
    }

    [Fact]
    public void Explorer_TargetSelection_DoesNotOverwriteEntityNameField()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AspireServiceBus.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var htmlPath = Path.Combine(directory!.FullName, "src", "AspireServiceBus.Sender", "wwwroot", "index.html");
        Assert.True(File.Exists(htmlPath), $"Expected composer markup at {htmlPath}");

        var html = File.ReadAllText(htmlPath);
        Assert.DoesNotContain("entityNameInput.value = entityName", html);
        Assert.Contains("Target:", html);
    }

    [Fact]
    public async Task Explorer_Entities_AreExposedThroughEndpoint()
    {
        var explorerClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Explorer:Entities:0:Name"] = "default-queue",
                    ["Explorer:Entities:0:Kind"] = "queue",
                    ["Explorer:Entities:0:Description"] = "Primary demo queue",
                    ["Explorer:Entities:1:Name"] = "orders-topic",
                    ["Explorer:Entities:1:Kind"] = "topic",
                    ["Explorer:Entities:1:Description"] = "Publish/subscribe topic"
                });
            });
        }).CreateClient();

        var response = await explorerClient.GetAsync("/explorer/entities");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("default-queue", payload.GetProperty("selectedEntity").GetString());

        var entities = payload.GetProperty("entities").EnumerateArray().ToList();
        Assert.Equal(2, entities.Count);
        Assert.Equal("default-queue", entities[0].GetProperty("name").GetString());
        Assert.Equal("queue", entities[0].GetProperty("kind").GetString());
        Assert.Equal("orders-topic", entities[1].GetProperty("name").GetString());
        Assert.Equal("topic", entities[1].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Explorer_QueueAndDeadLetterCounts_AreExposed()
    {
        var explorerClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Explorer:Entities:0:Name"] = "default-queue",
                    ["Explorer:Entities:0:Kind"] = "queue",
                    ["Explorer:Entities:1:Name"] = "default-queue/$DeadLetterQueue",
                    ["Explorer:Entities:1:Kind"] = "queue",
                    ["Explorer:Counts:default-queue"] = "7",
                    ["Explorer:Counts:default-queue/$DeadLetterQueue"] = "2"
                });
            });
        }).CreateClient();

        var response = await explorerClient.GetAsync("/explorer/entities");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entities = payload.GetProperty("entities").EnumerateArray().ToList();

        Assert.Single(entities);
        Assert.Equal("default-queue", entities[0].GetProperty("name").GetString());
        Assert.Equal(7, entities[0].GetProperty("count").GetInt64());
        Assert.Equal(2, entities[0].GetProperty("deadLetterCount").GetInt64());
    }

    [Fact]
    public async Task UpdateOutcome_ByServiceBusMessageId_TransitionsHistoryToSuccess()
    {
        var historyFilePath = Path.Combine(Path.GetTempPath(), $"sender-history-{Guid.NewGuid():N}.ndjson");
        var store = new FileMessageHistoryStore(historyFilePath, NullLogger<FileMessageHistoryStore>.Instance);

        var initialEntry = new MessageHistoryEntry(
            Id: Guid.NewGuid().ToString("N"),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            QueueName: "default-queue",
            Outcome: MessageHistoryOutcome.Processing,
            FailureReason: null,
            ServiceBusMessageId: "msg-123",
            Request: new SendMessageRequest("2026-08-08T00:00:00Z", "default-queue", "receiver", "{\"message\":\"hello\"}", new Dictionary<string, string>()),
            EffectiveHeaders: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BodyJson: "{\"message\":\"hello\"}");

        await store.AppendAsync(initialEntry, CancellationToken.None);
        await store.UpdateOutcomeByServiceBusMessageIdAsync("msg-123", MessageHistoryOutcome.Success, cancellationToken: CancellationToken.None);

        var successHistory = await store.GetByOutcomeAsync(MessageHistoryOutcome.Success, 20, 0, CancellationToken.None);
        var failureHistory = await store.GetByOutcomeAsync(MessageHistoryOutcome.Failed, 20, 0, CancellationToken.None);

        Assert.NotNull(successHistory);
        Assert.Single(successHistory.Items);
        Assert.Equal(MessageHistoryOutcome.Success, successHistory.Items[0].Outcome);
        Assert.Empty(failureHistory.Items);
    }

    [Fact]
    public async Task PurgeByOutcome_DoesNotLoseEntriesAppendedDuringPurge()
    {
        var historyFilePath = Path.Combine(Path.GetTempPath(), $"sender-history-{Guid.NewGuid():N}.ndjson");
        var store = new FileMessageHistoryStore(historyFilePath, NullLogger<FileMessageHistoryStore>.Instance);

        var successEntry = new MessageHistoryEntry(
            Id: Guid.NewGuid().ToString("N"),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            QueueName: "default-queue",
            Outcome: MessageHistoryOutcome.Success,
            FailureReason: null,
            ServiceBusMessageId: null,
            Request: new SendMessageRequest("2026-08-08T00:00:00Z", "default-queue", "receiver", "{\"message\":\"success\"}", new Dictionary<string, string>()),
            EffectiveHeaders: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BodyJson: "{\"message\":\"success\"}");

        await File.WriteAllTextAsync(historyFilePath, JsonSerializer.Serialize(successEntry) + Environment.NewLine);

        var appendedEntry = new MessageHistoryEntry(
            Id: Guid.NewGuid().ToString("N"),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            QueueName: "default-queue",
            Outcome: MessageHistoryOutcome.Failed,
            FailureReason: "appended-during-purge",
            ServiceBusMessageId: null,
            Request: new SendMessageRequest("2026-08-08T00:00:00Z", "default-queue", "receiver", "{\"message\":\"appended\"}", new Dictionary<string, string>()),
            EffectiveHeaders: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BodyJson: "{\"message\":\"appended\"}");

        var fileLockField = typeof(FileMessageHistoryStore).GetField("FileLock", BindingFlags.NonPublic | BindingFlags.Static);
        var fileLock = (SemaphoreSlim?)fileLockField?.GetValue(null);

        Assert.NotNull(fileLock);

        await fileLock!.WaitAsync(CancellationToken.None);
        try
        {
            var appendTask = store.AppendAsync(appendedEntry, CancellationToken.None);
            var purgeTask = store.PurgeByOutcomeAsync(MessageHistoryOutcome.Success, CancellationToken.None);

            await Task.Yield();
            fileLock.Release();

            await Task.WhenAll(appendTask, purgeTask);
        }
        finally
        {
            if (fileLock.CurrentCount == 0)
            {
                fileLock.Release();
            }
        }

        var remainingLines = await File.ReadAllLinesAsync(historyFilePath);
        var remainingEntries = remainingLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<MessageHistoryEntry>(line))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToList();

        Assert.DoesNotContain(remainingEntries, entry => entry.Outcome == MessageHistoryOutcome.Success);
        Assert.Contains(remainingEntries, entry => entry.Id == appendedEntry.Id);
    }

    [Fact]
    public async Task History_Endpoints_DefaultToPageSize_WhenQueryParamsOmitted()
    {
        await _client.PostAsJsonAsync("/send", new
        {
            timestamp = "2026-08-08T00:00:00Z",
            entityName = "default-queue",
            targetApplication = "receiver",
            bodyJson = "{\"message\":\"hello\"}",
            customHeaders = new Dictionary<string, string> { ["trace"] = "abc" }
        });

        var successHistoryResponse = await _client.GetAsync("/history/success");
        successHistoryResponse.EnsureSuccessStatusCode();

        var successHistory = await successHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();
        Assert.NotNull(successHistory);
        Assert.Empty(successHistory!.Items);

        var failedHistoryResponse = await _client.GetAsync("/history/failed");
        failedHistoryResponse.EnsureSuccessStatusCode();

        var failedHistory = await failedHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();
        Assert.NotNull(failedHistory);
        Assert.NotEmpty(failedHistory!.Items);
    }

    [Fact]
    public async Task Send_PreservesSelectedEntityNameInRequestAndHeaders()
    {
        var response = await _client.PostAsJsonAsync("/send", new
        {
            timestamp = "2026-08-08T00:00:00Z",
            entityName = "supplier",
            targetApplication = "receiver",
            bodyJson = "{\"message\":\"hello\"}",
            customHeaders = new Dictionary<string, string> { ["trace"] = "abc" }
        });

        Assert.True(response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.OK or HttpStatusCode.BadRequest);

        var failedHistoryResponse = await _client.GetAsync("/history/failed?take=20&skip=0");
        failedHistoryResponse.EnsureSuccessStatusCode();

        var failedHistory = await failedHistoryResponse.Content.ReadFromJsonAsync<MessageHistoryQueryResult>();
        Assert.NotNull(failedHistory);
        Assert.NotEmpty(failedHistory!.Items);

        var entry = failedHistory.Items[0];
        Assert.Equal("supplier", entry.Request.EntityName);
        Assert.Equal("supplier", entry.EffectiveHeaders["entity-name"]);
        Assert.False(entry.EffectiveHeaders.ContainsKey("entity-type"));
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
