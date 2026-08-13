using AspireServiceBus.Sender;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    Console.Error.WriteLine($"[sender] Unhandled exception: {eventArgs.ExceptionObject}");
};

TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    Console.Error.WriteLine($"[sender] Unobserved task exception: {eventArgs.Exception}");
    eventArgs.SetObserved();
};

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

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var queueName = builder.Configuration["ServiceBus:QueueName"] ?? "default-queue";
var localLogPath = builder.Configuration["Logging:LocalFilePath"] ?? Environment.GetEnvironmentVariable("SERVICEBUS_LOG_FILE");
var historyFilePath = builder.Configuration["History:FilePath"]
    ?? Environment.GetEnvironmentVariable("SERVICEBUS_HISTORY_FILE")
    ?? Path.Combine(AppContext.BaseDirectory, "data", "message-history.ndjson");
var connectionString = ServiceBusConnectionSettings.ResolveConnectionString(builder.Configuration);
var diagnostics = ServiceBusConnectionSettings.CreateDiagnostics(builder.Configuration, queueName, historyFilePath, localLogPath);

builder.Services.AddSingleton<IMessageHistoryStore>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<FileMessageHistoryStore>>();
    return new FileMessageHistoryStore(historyFilePath, logger);
});

builder.Services.AddSingleton<IExplorerEntityCatalog, ExplorerEntityCatalog>();

if (!string.IsNullOrWhiteSpace(connectionString))
{
    try
    {
        builder.Services.AddSingleton(new ServiceBusClient(connectionString));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[sender] Failed to initialize ServiceBusClient with connection string {connectionString}: {ex}");
    }
}

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SenderStartup");
startupLogger.LogInformation("Sender service starting with queue {QueueName}", queueName);
startupLogger.LogDebug("Sender configuration snapshot {@Configuration}", new
{
    queueName,
    resolvedConnectionString = diagnostics.ResolvedConnectionString,
    hasResolvedConnectionString = diagnostics.HasResolvedConnectionString,
    explicitConnectionString = diagnostics.ExplicitConnectionString,
    configuredConnectionString = diagnostics.ConfiguredConnectionString,
    historyFilePath,
    localLogPath
});

if (!diagnostics.HasResolvedConnectionString)
{
    startupLogger.LogWarning("No Service Bus connection string was resolved. The sender will continue running and return 503 until a connection becomes available.");
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    startupLogger.LogInformation("Sender is ready to accept traffic on queue {QueueName}", queueName);
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapSenderEndpoints(queueName, localLogPath);

try
{
    app.Run();
}
catch (Exception ex)
{
    var failureLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SenderStartup");
    failureLogger.LogCritical(ex, "Sender process terminated unexpectedly");
    failureLogger.LogCritical("Sender failure snapshot {@FailureSnapshot}", CreateFailureSnapshot(builder.Configuration, queueName, historyFilePath, localLogPath, connectionString));
    throw;
}

public partial class Program
{
    private static object CreateFailureSnapshot(IConfiguration configuration, string queueName, string historyFilePath, string? localLogPath, string? connectionString)
    {
        return new
        {
            processId = Environment.ProcessId,
            workingDirectory = Environment.CurrentDirectory,
            frameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            queueName,
            historyFilePath,
            localLogPath,
            resolvedConnectionString = connectionString,
            explicitConnectionString = configuration["ConnectionStrings__servicebus"] ?? configuration["ConnectionStrings:servicebus"],
            configuredConnectionString = configuration.GetConnectionString("servicebus"),
            environment = Environment.GetEnvironmentVariables().Keys.Cast<object>()
                .OrderBy(key => key.ToString())
                .Where(key => key.ToString() is not null && (key.ToString()!.Contains("SERVICEBUS", StringComparison.OrdinalIgnoreCase) || key.ToString()!.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase) || key.ToString()!.Contains("ASPNETCORE", StringComparison.OrdinalIgnoreCase) || key.ToString()!.Contains("DOTNET", StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(key => key.ToString()!, key => Environment.GetEnvironmentVariable(key.ToString()!) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        };
    }
}
