using AspireServiceBus.Receiver;
using AspireServiceBus.Sender;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var hostEndpoint = Environment.GetEnvironmentVariable("Functions__Worker__HostEndpoint")
    ?? Environment.GetEnvironmentVariable("FUNCTIONS__WORKER__HOSTENDPOINT")
    ?? Environment.GetEnvironmentVariable("Functions:Worker:HostEndpoint")
    ?? Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_HOST_ENDPOINT");

if (string.IsNullOrWhiteSpace(hostEndpoint) || hostEndpoint == "http://:")
{
    Console.WriteLine("Azure Functions host endpoint is not available. The receiver will stay idle until it is started by the Functions host runtime.");

    var fallbackHost = Host.CreateDefaultBuilder(args)
        .ConfigureServices(services =>
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

            services.AddHostedService<ServiceBusProcessingBackgroundService>();
        })
        .Build();

    await fallbackHost.RunAsync();
    return;
}

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
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

        services.AddHostedService<ServiceBusProcessingBackgroundService>();
    })
    .Build();

host.Run();
