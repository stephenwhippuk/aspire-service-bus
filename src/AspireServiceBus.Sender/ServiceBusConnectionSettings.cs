using Microsoft.Extensions.Configuration;

namespace AspireServiceBus.Sender;

public static class ServiceBusConnectionSettings
{
    public static string? ResolveConnectionString(IConfiguration configuration)
    {
        return configuration.GetConnectionString("servicebus")
            ?? configuration["ConnectionStrings__servicebus"];
    }
}
