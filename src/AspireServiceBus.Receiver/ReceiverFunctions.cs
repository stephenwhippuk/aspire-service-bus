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
        [ServiceBusTrigger("%ServiceBus:QueueName%", Connection = "servicebus")] ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        var queueName = Environment.GetEnvironmentVariable("ServiceBus__QueueName") ?? "default-queue";
        var headers = message.ApplicationProperties
            .Where(entry => entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var payload = new ReceivedMessageEnvelope(message.MessageId, message.Body.ToString(), headers);
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
        _logger.LogInformation("Received payload: {Payload}", JsonSerializer.Serialize(input));
        return Task.FromResult($"processed:{input.MessageId}");
    }
}
