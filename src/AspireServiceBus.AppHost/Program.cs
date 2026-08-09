var builder = DistributedApplication.CreateBuilder(args);

var sharedDataDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".data"));
var senderHistoryFilePath = Path.Combine(sharedDataDirectory, "sender-history.ndjson");

var serviceBus = builder
	.AddAzureServiceBus("servicebus")
	.RunAsEmulator();

serviceBus.AddServiceBusQueue("default-queue");

builder.AddProject<Projects.AspireServiceBus_Sender>("sender")
	.WithReference(serviceBus)
	.WaitFor(serviceBus)
	.WithEnvironment("SERVICEBUS_HISTORY_FILE", senderHistoryFilePath)
	.WithExternalHttpEndpoints();

builder.AddProject<Projects.AspireServiceBus_Receiver>("receiver")
	.WithReference(serviceBus)
	.WaitFor(serviceBus)
	.WithEnvironment("AzureWebJobsStorage", "UseDevelopmentStorage=true")
	.WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
	.WithEnvironment("ServiceBus__QueueName", "default-queue");

builder.Build().Run();
