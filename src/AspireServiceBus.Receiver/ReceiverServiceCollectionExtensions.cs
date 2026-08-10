using AspireServiceBus.Sender;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspireServiceBus.Receiver;

public static class ReceiverServiceCollectionExtensions
{
    public static IServiceCollection AddReceiverServices(this IServiceCollection services, bool enableBackgroundProcessor)
    {
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSimpleConsole(options =>
            {
                options.SingleLine = false;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });

        var historyFilePath = Environment.GetEnvironmentVariable("SERVICEBUS_HISTORY_FILE")
            ?? Path.Combine(AppContext.BaseDirectory, "data", "message-history.ndjson");

        services.AddSingleton<IMessageHistoryStore>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<FileMessageHistoryStore>>();
            return new FileMessageHistoryStore(historyFilePath, logger);
        });

        if (enableBackgroundProcessor)
        {
            services.AddHostedService<ServiceBusProcessingBackgroundService>();
        }

        return services;
    }
}
