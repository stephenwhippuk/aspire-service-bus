using AspireServiceBus.Receiver;
using Azure.Messaging.ServiceBus;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var connectionString = builder.Configuration.GetConnectionString("servicebus")
	?? builder.Configuration["ConnectionStrings__servicebus"];

if (string.IsNullOrWhiteSpace(connectionString))
{
	builder.Services.AddSingleton<ServiceBusClient>(_ => null!);
}
else
{
	builder.Services.AddSingleton(new ServiceBusClient(connectionString));
}

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
