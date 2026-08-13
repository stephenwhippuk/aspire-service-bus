using Microsoft.Extensions.Configuration;

namespace AspireServiceBus.Receiver;

public static class ReceiverConfiguration
{
    public static string? ResolveServiceBusConnectionString(IConfiguration configuration)
    {
        return configuration["ConnectionStrings__servicebus"]
            ?? configuration["ConnectionStrings:servicebus"]
            ?? configuration["AzureWebJobsServiceBus"]
            ?? configuration["ServiceBus:ConnectionString"]
            ?? configuration.GetConnectionString("servicebus");
    }

    public static string ResolveQueueName(IConfiguration configuration)
    {
        return configuration["ServiceBus:QueueName"]
            ?? configuration["ServiceBus__QueueName"]
            ?? "default-queue";
    }
}
