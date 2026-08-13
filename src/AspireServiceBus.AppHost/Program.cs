using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using AspireServiceBus.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var sharedDataDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".data"));
var senderHistoryFilePath = Path.Combine(sharedDataDirectory, "sender-history.ndjson");

var serviceBus = builder
	.AddAzureServiceBus("servicebus")
	.RunAsEmulator(emulator => emulator.WithHostPort(39500));

var storage = builder
	.AddAzureStorage("storage")
	.RunAsEmulator();

serviceBus.AddServiceBusQueue("default-queue");

var serviceBusConnectionString = GetServiceBusConnectionString(serviceBus.Resource);
var storageConnectionString = GetStorageConnectionString(storage.Resource);

Console.WriteLine($"[apphost] Injecting Service Bus connection string: {serviceBusConnectionString}");
Console.WriteLine($"[apphost] Injecting Storage connection string: {storageConnectionString}");
Console.WriteLine($"[apphost] Resolved Service Bus endpoint host='{ResolveServiceBusHostAddress()}' port='{ResolveServiceBusHostPort()}'");
Console.WriteLine($"[apphost] Service Bus endpoint host={serviceBus.Resource.GetType().GetProperty("HostName")?.GetValue(serviceBus.Resource)?.ToString() ?? "<unknown>"} port={serviceBus.Resource.GetType().GetProperty("Port")?.GetValue(serviceBus.Resource)?.ToString() ?? "<unknown>"}");

builder.AddProject<Projects.AspireServiceBus_Sender>("sender")
	.WithReference(serviceBus)
	.WithEnvironment("SERVICEBUS_HISTORY_FILE", senderHistoryFilePath)
	.WithEnvironment("ConnectionStrings__servicebus", serviceBusConnectionString)
	.WithExternalHttpEndpoints();

builder.AddProject<Projects.AspireServiceBus_Receiver>("receiver")
	.WithReference(serviceBus)
	.WithEnvironment("AzureWebJobsStorage", storageConnectionString!)
	.WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
	.WithEnvironment("ServiceBus__QueueName", "default-queue")
	.WithEnvironment("AzureWebJobsServiceBus", serviceBusConnectionString)
	.WithEnvironment("ConnectionStrings__servicebus", serviceBusConnectionString)
	.WithEnvironment("SERVICEBUS_HISTORY_FILE", senderHistoryFilePath);

builder.Build().Run();

static ReferenceExpression? GetStorageConnectionString(object resource)
{
    var method = resource.GetType().GetMethod("GetEmulatorConnectionString", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    return method?.Invoke(resource, null) as ReferenceExpression;
}

static string GetServiceBusConnectionString(object resource)
{
    return ServiceBusConnectionStringBuilder.BuildConnectionStringForResource(
        resource,
        resolveHost: ResolveServiceBusHostAddress,
        resolvePort: ResolveServiceBusHostPort);
}

static string? ResolveServiceBusHostAddress()
{
    var configuredHost = Environment.GetEnvironmentVariable("SERVICEBUS_HOST")
        ?? Environment.GetEnvironmentVariable("SERVICEBUS_URI");

    if (!string.IsNullOrWhiteSpace(configuredHost))
    {
        if (Uri.TryCreate(configuredHost, UriKind.Absolute, out var serviceBusUri))
        {
            return serviceBusUri.Host;
        }

        return configuredHost;
    }

    return "127.0.0.1";
}

static string? ResolveServiceBusHostPort()
{
    var configuredPort = Environment.GetEnvironmentVariable("SERVICEBUS_PORT");
    if (!string.IsNullOrWhiteSpace(configuredPort) && int.TryParse(configuredPort, out var configuredPortNumber) && configuredPortNumber > 0)
    {
        return configuredPortNumber.ToString();
    }

    var configuredUri = Environment.GetEnvironmentVariable("SERVICEBUS_URI");
    if (!string.IsNullOrWhiteSpace(configuredUri) && Uri.TryCreate(configuredUri, UriKind.Absolute, out var serviceBusUri) && serviceBusUri.Port > 0)
    {
        return serviceBusUri.Port.ToString();
    }

    return "39500";
}

