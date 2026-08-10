namespace AspireServiceBus.Sender;

public interface IMessageHistoryStore
{
    Task AppendAsync(MessageHistoryEntry entry, CancellationToken cancellationToken);
    Task<MessageHistoryQueryResult> GetByOutcomeAsync(string outcome, int take, int skip, CancellationToken cancellationToken);
    Task<MessageHistoryEntry?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task PurgeByOutcomeAsync(string outcome, CancellationToken cancellationToken);
    Task UpdateOutcomeByServiceBusMessageIdAsync(string serviceBusMessageId, string outcome, string? failureReason = null, CancellationToken cancellationToken = default);
}