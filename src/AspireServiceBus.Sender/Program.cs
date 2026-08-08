using AspireServiceBus.Sender;
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var queueName = builder.Configuration["ServiceBus:QueueName"] ?? "default-queue";
var connectionString = ServiceBusConnectionSettings.ResolveConnectionString(builder.Configuration);
var localLogPath = builder.Configuration["Logging:LocalFilePath"] ?? Environment.GetEnvironmentVariable("SERVICEBUS_LOG_FILE");

if (string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton<ServiceBusClient>(_ => null!);
}
else
{
    builder.Services.AddSingleton(new ServiceBusClient(connectionString));
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapSenderEndpoints(queueName, localLogPath);

app.Run();
