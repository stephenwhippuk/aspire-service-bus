var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder
	.AddAzureServiceBus("servicebus")
	.RunAsEmulator();

serviceBus.AddServiceBusQueue("default-queue");

builder.AddProject<Projects.AspireServiceBus_Sender>("sender")
	.WithReference(serviceBus)
	.WaitFor(serviceBus)
	.WithExternalHttpEndpoints();

builder.AddProject<Projects.AspireServiceBus_Receiver>("receiver")
	.WithReference(serviceBus)
	.WaitFor(serviceBus);

builder.Build().Run();
