using AspireServiceBus.Sender;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = false;
    options.TimestampFormat = "HH:mm:ss ";
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var queueName = builder.Configuration["ServiceBus:QueueName"] ?? "default-queue";
var connectionString = ServiceBusConnectionSettings.ResolveConnectionString(builder.Configuration);
var localLogPath = builder.Configuration["Logging:LocalFilePath"] ?? Environment.GetEnvironmentVariable("SERVICEBUS_LOG_FILE");
var historyFilePath = builder.Configuration["History:FilePath"]
    ?? Environment.GetEnvironmentVariable("SERVICEBUS_HISTORY_FILE")
    ?? Path.Combine(AppContext.BaseDirectory, "data", "message-history.ndjson");

builder.Services.AddSingleton<IMessageHistoryStore>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<FileMessageHistoryStore>>();
    return new FileMessageHistoryStore(historyFilePath, logger);
});

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton(new ServiceBusClient(connectionString));
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapSenderEndpoints(queueName, localLogPath);

app.Run();

public partial class Program
{
}
