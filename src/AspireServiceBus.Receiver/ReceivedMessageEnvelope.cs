namespace AspireServiceBus.Receiver;

public sealed record ReceivedMessageEnvelope(
    string MessageId,
    string Body,
    IReadOnlyDictionary<string, string> Headers,
    string? EntityName = null);
