using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace AspireServiceBus.Receiver;

public sealed class ReceiverFunctions
{
    private readonly ILogger<ReceiverFunctions> _logger;

    public ReceiverFunctions(ILogger<ReceiverFunctions> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ServiceBusQueueToDurableStarter))]
    public async Task ServiceBusQueueToDurableStarter(
        [ServiceBusTrigger("%ServiceBus:QueueName%", Connection = "AzureWebJobsServiceBus")] ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        var queueName = Environment.GetEnvironmentVariable("ServiceBus__QueueName") ?? "default-queue";
        var headers = message.ApplicationProperties
            .Where(entry => entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var entityName = headers.TryGetValue("entity-name", out var entityNameValue) && !string.IsNullOrWhiteSpace(entityNameValue)
            ? entityNameValue
            : "store";

        _logger.LogInformation("Received message {MessageId} for entity-name {EntityName}", message.MessageId, entityName);
        _logger.LogDebug("Received Service Bus message payload {@Payload}", new
        {
            MessageId = message.MessageId,
            MessageBody = message.Body.ToString(),
            ApplicationProperties = message.ApplicationProperties.ToDictionary(entry => entry.Key, entry => entry.Value?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        });
        await AppendEventAsync($"trigger:{message.MessageId}:entity={entityName}");

        var payload = new ReceivedMessageEnvelope(message.MessageId, message.Body.ToString(), headers, entityName);
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(ProcessMessageOrchestrator),
            payload);

        _logger.LogInformation("Started orchestration {InstanceId} for message {MessageId} on queue {QueueName}", instanceId, message.MessageId, queueName);
    }

    [Function(nameof(ProcessMessageOrchestrator))]
    public static Task<string> ProcessMessageOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<ReceivedMessageEnvelope>()
            ?? throw new InvalidOperationException("No message payload was supplied to the orchestration.");

        return context.CallActivityAsync<string>(nameof(ProcessMessageActivity), input);
    }

    [Function(nameof(ProcessMessageActivity))]
    public Task<string> ProcessMessageActivity([ActivityTrigger] ReceivedMessageEnvelope input)
    {
        _logger.LogInformation("Processing message {MessageId}", input.MessageId);
        _logger.LogDebug("Received orchestration payload {@Payload}", input);

        var entityName = input.EntityName ?? input.Headers.GetValueOrDefault("entity-name") ?? "store";
        if (!string.Equals(entityName, "store", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Message {MessageId} is not a store message. It will be retried and dead-lettered.", input.MessageId);
            AppendEventAsync($"activity-fail:{input.MessageId}:entity={entityName}").GetAwaiter().GetResult();
            throw new InvalidOperationException($"Message {input.MessageId} is not a store message and should be dead-lettered.");
        }

        _logger.LogInformation("Processing store message {MessageId}", input.MessageId);
        AppendEventAsync($"activity-pass:{input.MessageId}:entity={entityName}").GetAwaiter().GetResult();
        return Task.FromResult($"processed:{input.MessageId}");
    }

    private static async Task AppendEventAsync(string message)
    {
        try
        {
            var logPath = Environment.GetEnvironmentVariable("SERVICEBUS_RECEIVER_LOG_FILE") ?? "/tmp/receiver-events.log";
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.AppendAllTextAsync(logPath, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[receiver] Failed to append receiver event log: {ex}");
        }
    }
}
