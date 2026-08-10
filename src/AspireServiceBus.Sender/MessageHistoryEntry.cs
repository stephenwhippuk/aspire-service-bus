namespace AspireServiceBus.Sender;

public static class MessageHistoryOutcome
{
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Processing = "processing";
}

public sealed record MessageHistoryEntry(
    string Id,
    DateTimeOffset CreatedAtUtc,
    string QueueName,
    string Outcome,
    string? FailureReason,
    string? ServiceBusMessageId,
    SendMessageRequest Request,
    IReadOnlyDictionary<string, string> EffectiveHeaders,
    string BodyJson,
    string? SourceAttemptId = null,
    bool IsResend = false);

public sealed record MessageHistoryQueryResult(
    IReadOnlyList<MessageHistoryEntry> Items,
    int Total,
    int Take,
    int Skip);