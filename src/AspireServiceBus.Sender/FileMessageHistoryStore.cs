using System.Text.Json;

namespace AspireServiceBus.Sender;

public sealed class FileMessageHistoryStore : IMessageHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly SemaphoreSlim FileLock = new(1, 1);

    private readonly string _historyFilePath;
    private readonly ILogger<FileMessageHistoryStore> _logger;

    public FileMessageHistoryStore(string historyFilePath, ILogger<FileMessageHistoryStore> logger)
    {
        _historyFilePath = historyFilePath;
        _logger = logger;
    }

    public async Task AppendAsync(MessageHistoryEntry entry, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_historyFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_historyFilePath, line, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<MessageHistoryQueryResult> GetByOutcomeAsync(string outcome, int take, int skip, CancellationToken cancellationToken)
    {
        var normalizedOutcome = NormalizeOutcome(outcome);
        var boundedTake = Math.Clamp(take, 1, 200);
        var boundedSkip = Math.Max(skip, 0);

        if (!File.Exists(_historyFilePath))
        {
            return new MessageHistoryQueryResult(Array.Empty<MessageHistoryEntry>(), 0, boundedTake, boundedSkip);
        }

        var lines = await File.ReadAllLinesAsync(_historyFilePath, cancellationToken);
        var items = new List<MessageHistoryEntry>(lines.Length);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<MessageHistoryEntry>(line, SerializerOptions);
                if (entry is null)
                {
                    continue;
                }

                if (NormalizeOutcome(entry.Outcome) == normalizedOutcome)
                {
                    items.Add(entry);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skipping malformed history line in {HistoryFilePath}", _historyFilePath);
            }
        }

        var ordered = items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        var page = ordered
            .Skip(boundedSkip)
            .Take(boundedTake)
            .ToList();

        return new MessageHistoryQueryResult(page, ordered.Count, boundedTake, boundedSkip);
    }

    public async Task<MessageHistoryEntry?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (!File.Exists(_historyFilePath))
        {
            return null;
        }

        var lines = await File.ReadAllLinesAsync(_historyFilePath, cancellationToken);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<MessageHistoryEntry>(line, SerializerOptions);
                if (entry is not null && string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            catch (JsonException)
            {
                // Ignore malformed entries and continue.
            }
        }

        return null;
    }

    public async Task PurgeByOutcomeAsync(string outcome, CancellationToken cancellationToken)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_historyFilePath))
            {
                return;
            }

            var normalizedOutcome = NormalizeOutcome(outcome);
            var lines = await File.ReadAllLinesAsync(_historyFilePath, cancellationToken);
            var remainingLines = new List<string>(lines.Length);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<MessageHistoryEntry>(line, SerializerOptions);
                    if (entry is null)
                    {
                        remainingLines.Add(line);
                        continue;
                    }

                    if (NormalizeOutcome(entry.Outcome) == normalizedOutcome)
                    {
                        continue;
                    }

                    remainingLines.Add(line);
                }
                catch (JsonException)
                {
                    remainingLines.Add(line);
                }
            }

            await File.WriteAllLinesAsync(_historyFilePath, remainingLines, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task UpdateOutcomeByServiceBusMessageIdAsync(string serviceBusMessageId, string outcome, string? failureReason = null, CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(serviceBusMessageId) || !File.Exists(_historyFilePath))
            {
                return;
            }

            var lines = await File.ReadAllLinesAsync(_historyFilePath, cancellationToken);
            var changed = false;

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<MessageHistoryEntry>(line, SerializerOptions);
                    if (entry is null)
                    {
                        continue;
                    }

                    if (!string.Equals(entry.ServiceBusMessageId, serviceBusMessageId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var updatedEntry = entry with
                    {
                        Outcome = outcome,
                        FailureReason = failureReason
                    };

                    lines[index] = JsonSerializer.Serialize(updatedEntry, SerializerOptions);
                    changed = true;
                }
                catch (JsonException)
                {
                    // Ignore malformed lines and continue.
                }
            }

            if (!changed)
            {
                return;
            }

            await File.WriteAllLinesAsync(_historyFilePath, lines, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private static string NormalizeOutcome(string outcome)
    {
        return outcome.Trim().ToLowerInvariant();
    }
}