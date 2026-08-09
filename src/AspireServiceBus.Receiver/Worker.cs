using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspireServiceBus.Receiver;

public class Worker : BackgroundService
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<Worker> _logger;
    private readonly ServiceBusClient? _client;
    private readonly IConfiguration _configuration;
    private readonly string? _localLogPath;

    public Worker(ILogger<Worker> logger, ServiceBusClient? client, IConfiguration configuration)
    {
        _logger = logger;
        _client = client;
        _configuration = configuration;
        _localLogPath = configuration["Logging:LocalFilePath"] ?? Environment.GetEnvironmentVariable("SERVICEBUS_LOG_FILE");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueName = _configuration["ServiceBus:QueueName"] ?? "default-queue";

        if (_client is null)
        {
            _logger.LogWarning("Service Bus connection string is unavailable. Receiver will stay idle until the emulator connection is configured.");
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }

            return;
        }

        var processor = _client.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var bodyText = args.Message.Body.ToString();
                using var bodyJson = JsonDocument.Parse(bodyText);

                var logPayload = new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    queue = queueName,
                    messageId = args.Message.MessageId,
                    headers = args.Message.ApplicationProperties
                        .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(entry => entry.Key, entry => entry.Value),
                    body = bodyJson.RootElement.Clone()
                };

                var prettyPayload = JsonSerializer.Serialize(logPayload, PrettyJsonOptions);

                _logger.LogInformation("Received message {MessageId} on queue {QueueName}", args.Message.MessageId, queueName);
                _logger.LogInformation("=== Received message payload ===");
                _logger.LogInformation(prettyPayload);
                await AppendLocalLogAsync(prettyPayload, stoppingToken);
                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Received message {MessageId} had invalid JSON body. Body preview: {BodyPreview}",
                    args.Message.MessageId,
                    args.Message.Body.ToString());
                await AppendLocalLogAsync(new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    service = "receiver",
                    action = "invalid-json",
                    messageId = args.Message.MessageId,
                    bodyPreview = args.Message.Body.ToString()
                }, stoppingToken);
                await args.AbandonMessageAsync(args.Message, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {MessageId}", args.Message.MessageId);
                await AppendLocalLogAsync(new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    service = "receiver",
                    action = "processing-error",
                    messageId = args.Message.MessageId,
                    error = ex.Message
                }, stoppingToken);
                await args.AbandonMessageAsync(args.Message, cancellationToken: stoppingToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception,
                "Service Bus processor error. Entity: {EntityPath}, Source: {ErrorSource}",
                args.EntityPath,
                args.ErrorSource);
            return Task.CompletedTask;
        };

        _logger.LogInformation("Starting receiver for queue {QueueName}", queueName);
        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        finally
        {
            await processor.StopProcessingAsync(CancellationToken.None);
            await processor.DisposeAsync();
        }
    }

    private async Task AppendLocalLogAsync(object payload, CancellationToken cancellationToken)
    {
        var prettyPayload = JsonSerializer.Serialize(payload, PrettyJsonOptions);
        await AppendLocalLogAsync(prettyPayload, cancellationToken);
    }

    private async Task AppendLocalLogAsync(string serializedPayload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_localLogPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_localLogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(_localLogPath, serializedPayload + Environment.NewLine + Environment.NewLine, cancellationToken);
    }
}
