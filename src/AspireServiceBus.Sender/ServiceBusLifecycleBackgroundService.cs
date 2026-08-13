using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspireServiceBus.Sender;

public sealed class ServiceBusLifecycleBackgroundService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceBusLifecycleBackgroundService> _logger;
    private readonly HistoryUpdateBroadcaster _historyUpdateBroadcaster;

    public ServiceBusLifecycleBackgroundService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<ServiceBusLifecycleBackgroundService> logger,
        HistoryUpdateBroadcaster historyUpdateBroadcaster)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _historyUpdateBroadcaster = historyUpdateBroadcaster;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueName = _configuration["ServiceBus:QueueName"] ?? _configuration["ServiceBus__QueueName"] ?? "default-queue";
        var client = _serviceProvider.GetService<ServiceBusClient>();

        if (client is null)
        {
            _logger.LogWarning("Service Bus client is not available, lifecycle polling will stay idle until the sender connection is configured.");
            return;
        }

        _logger.LogInformation("Starting Service Bus lifecycle polling for queue {QueueName}", queueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(client, queueName, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Service Bus lifecycle polling failed for queue {QueueName}", queueName);
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task PollAsync(ServiceBusClient client, string queueName, CancellationToken cancellationToken)
    {
        var historyStore = _serviceProvider.GetRequiredService<IMessageHistoryStore>();
        var mainQueueMessageIds = await PeekMessageIdsAsync(client, queueName, cancellationToken);
        var deadLetterMessageIds = await PeekMessageIdsAsync(client, $"{queueName}/$DeadLetterQueue", cancellationToken);

        var pendingHistory = await historyStore.GetByOutcomeAsync(MessageHistoryOutcome.Pending, 200, 0, cancellationToken);
        var processingHistory = await historyStore.GetByOutcomeAsync(MessageHistoryOutcome.Processing, 200, 0, cancellationToken);

        var changed = false;
        foreach (var entry in pendingHistory.Items.Concat(processingHistory.Items))
        {
            if (string.IsNullOrWhiteSpace(entry.ServiceBusMessageId))
            {
                continue;
            }

            if (deadLetterMessageIds.Contains(entry.ServiceBusMessageId))
            {
                await historyStore.UpdateOutcomeByServiceBusMessageIdAsync(entry.ServiceBusMessageId, MessageHistoryOutcome.Failed, "The message was observed in the dead-letter queue.", cancellationToken);
                changed = true;
                continue;
            }

            if (mainQueueMessageIds.Contains(entry.ServiceBusMessageId))
            {
                continue;
            }

            if (string.Equals(entry.Outcome, MessageHistoryOutcome.Pending, StringComparison.OrdinalIgnoreCase))
            {
                await historyStore.UpdateOutcomeByServiceBusMessageIdAsync(entry.ServiceBusMessageId, MessageHistoryOutcome.Processing, cancellationToken: cancellationToken);
                changed = true;
                continue;
            }

            var transitionTime = entry.StateUpdatedAtUtc ?? entry.CreatedAtUtc;
            var waitTimeSeconds = entry.Request.WaitTimeSeconds ?? 0;
            if (waitTimeSeconds <= 0 || DateTimeOffset.UtcNow - transitionTime >= TimeSpan.FromSeconds(waitTimeSeconds))
            {
                await historyStore.UpdateOutcomeByServiceBusMessageIdAsync(entry.ServiceBusMessageId, MessageHistoryOutcome.Success, cancellationToken: cancellationToken);
                changed = true;
            }
        }

        if (changed)
        {
            await _historyUpdateBroadcaster.BroadcastAsync(new { type = "history-changed", reason = "state-updated" }, cancellationToken);
        }
    }

    private static async Task<HashSet<string>> PeekMessageIdsAsync(ServiceBusClient client, string entityPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

            var messages = await receiver.PeekMessagesAsync(maxMessages: 200, cancellationToken: cancellationToken);
            return messages
                .Select(message => message.MessageId)
                .Where(messageId => !string.IsNullOrWhiteSpace(messageId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
