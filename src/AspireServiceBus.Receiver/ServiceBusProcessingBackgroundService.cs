using AspireServiceBus.Sender;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspireServiceBus.Receiver;

public sealed class ServiceBusProcessingBackgroundService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IMessageHistoryStore _historyStore;
    private readonly ILogger<ServiceBusProcessingBackgroundService> _logger;

    public ServiceBusProcessingBackgroundService(
        IConfiguration configuration,
        IMessageHistoryStore historyStore,
        ILogger<ServiceBusProcessingBackgroundService> logger)
    {
        _configuration = configuration;
        _historyStore = historyStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = _configuration.GetConnectionString("servicebus")
            ?? _configuration["ConnectionStrings__servicebus"];
        var queueName = _configuration["ServiceBus:QueueName"]
            ?? _configuration["ServiceBus__QueueName"]
            ?? "default-queue";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Service Bus connection string is not available. Message processing will remain idle until the emulator connection is configured.");
            return;
        }

        await using var client = new ServiceBusClient(connectionString);
        await using var processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var bodyText = args.Message.Body.ToString();
                _logger.LogInformation("Processing service bus message {MessageId} from queue {QueueName}. Body: {Body}", args.Message.MessageId, queueName, bodyText);

                await _historyStore.UpdateOutcomeByServiceBusMessageIdAsync(args.Message.MessageId, MessageHistoryOutcome.Success, cancellationToken: stoppingToken);
                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {MessageId}", args.Message.MessageId);
                await _historyStore.UpdateOutcomeByServiceBusMessageIdAsync(args.Message.MessageId, MessageHistoryOutcome.Failed, ex.Message, stoppingToken);
                await args.AbandonMessageAsync(args.Message, cancellationToken: stoppingToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Service Bus processor error while handling {EntityPath}", args.EntityPath);
            return Task.CompletedTask;
        };

        _logger.LogInformation("Starting background Service Bus processor for queue {QueueName}", queueName);
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
        }
    }
}
