using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspireServiceBus.Sender;

public interface IExplorerEntityCatalog
{
    ExplorerEntitiesResponse GetResponse();
}

public sealed class ExplorerEntityCatalog(IConfiguration configuration, ILogger<ExplorerEntityCatalog> logger, IServiceProvider serviceProvider) : IExplorerEntityCatalog
{
    public ExplorerEntitiesResponse GetResponse()
    {
        var entities = GetConfiguredEntities();
        var selectedEntity = entities.FirstOrDefault()?.Name ?? "default-queue";

        var enrichedEntities = entities
            .Select(entity =>
            {
                var (queueCount, deadLetterCount) = GetResolvedCounts(entity.Name);
                return entity with { Count = queueCount, DeadLetterCount = deadLetterCount };
            })
            .ToList();

        return new ExplorerEntitiesResponse(selectedEntity, enrichedEntities);
    }

    private IReadOnlyList<ExplorerEntity> GetConfiguredEntities()
    {
        var configuredEntities = configuration
            .GetSection("Explorer:Entities")
            .GetChildren()
            .Select(item =>
            {
                var name = item["Name"] ?? item["name"] ?? string.Empty;
                var kind = item["Kind"] ?? item["kind"] ?? "queue";
                var description = item["Description"] ?? item["description"] ?? $"Configured {kind}";

                return new ExplorerEntity(name.Trim(), kind.Trim().ToLowerInvariant(), description.Trim());
            })
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Name))
            .ToList();

        if (configuredEntities.Count > 0)
        {
            return configuredEntities
                .Where(entity => !IsDeadLetterEntity(entity.Name))
                .ToList();
        }

        var queueName = configuration["ServiceBus:QueueName"] ?? "default-queue";

        return
        [
            new ExplorerEntity(queueName, "queue", "Primary queue from the current Service Bus configuration.")
        ];
    }

    private (long? QueueCount, long? DeadLetterCount) GetResolvedCounts(string entityName)
    {
        var queueName = NormalizeQueueName(entityName);
        var queueCount = GetConfiguredCount(entityName) ?? GetLiveCount(queueName, SubQueue.None) ?? 0;
        var deadLetterCount = GetConfiguredCount(GetDeadLetterEntityName(queueName)) ?? GetLiveCount(queueName, SubQueue.DeadLetter) ?? 0;
        return (queueCount, deadLetterCount);
    }

    private long? GetConfiguredCount(string entityName)
    {
        var configuredValue = configuration[$"Explorer:Counts:{entityName}"];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return null;
        }

        return long.TryParse(configuredValue, out var count) ? count : null;
    }

    private long? GetLiveCount(string queueName, SubQueue subQueue)
    {
        var client = serviceProvider.GetService<ServiceBusClient>();
        if (client is null)
        {
            return null;
        }

        try
        {
            var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                SubQueue = subQueue
            });

            var peekedMessages = receiver.PeekMessagesAsync(maxMessages: 100, cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
            return peekedMessages.Count;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to resolve entity count for {QueueName} with subqueue {SubQueue}.", queueName, subQueue);
            return null;
        }
    }

    private static bool IsDeadLetterEntity(string entityName)
    {
        return entityName.EndsWith("/$DeadLetterQueue", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeQueueName(string entityName)
    {
        return IsDeadLetterEntity(entityName)
            ? entityName[..^"/$DeadLetterQueue".Length]
            : entityName;
    }

    private static string GetDeadLetterEntityName(string queueName)
    {
        return $"{queueName}/$DeadLetterQueue";
    }
}

public sealed record ExplorerEntity(string Name, string Kind, string Description, long? Count = null, long? DeadLetterCount = null);

public sealed record ExplorerEntitiesResponse(string SelectedEntity, IReadOnlyList<ExplorerEntity> Entities);
