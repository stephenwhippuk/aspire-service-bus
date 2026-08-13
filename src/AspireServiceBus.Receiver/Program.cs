using AspireServiceBus.Receiver;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    Console.Error.WriteLine($"[receiver] Unhandled exception: {eventArgs.ExceptionObject}");
};

TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    Console.Error.WriteLine($"[receiver] Unobserved task exception: {eventArgs.Exception}");
    eventArgs.SetObserved();
};

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = false;
    options.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("AspireServiceBus", LogLevel.Debug);

var queueName = ReceiverConfiguration.ResolveQueueName(builder.Configuration);
var serviceBusConnectionString = ReceiverConfiguration.ResolveServiceBusConnectionString(builder.Configuration);
var localLogPath = builder.Configuration["Logging:LocalFilePath"] ?? Environment.GetEnvironmentVariable("SERVICEBUS_LOG_FILE");

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

if (!string.IsNullOrWhiteSpace(serviceBusConnectionString))
{
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnectionString));
}

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ReceiverStartup");
startupLogger.LogInformation("Receiver service starting with queue {QueueName}", queueName);
startupLogger.LogDebug("Receiver startup configuration {@Configuration}", new
{
    queueName,
    serviceBusConnectionString,
    hasServiceBusConnectionString = !string.IsNullOrWhiteSpace(serviceBusConnectionString),
    localLogPath,
    functionsWorkerRuntime = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME")
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", queue = queueName }));
app.MapGet("/", () => Results.Ok(new { status = "ready", queue = queueName }));

app.Run();
