using AspireServiceBus.Receiver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspireServiceBus.Receiver.Tests;

public class ReceiverServiceRegistrationTests
{
    [Fact]
    public void AddReceiverServices_DoesNotRegisterBackgroundService_WhenDisabled()
    {
        var services = new ServiceCollection();

        services.AddReceiverServices(enableBackgroundProcessor: false);

        var descriptor = services.SingleOrDefault(service => service.ServiceType == typeof(ServiceBusProcessingBackgroundService));
        Assert.Null(descriptor);
    }

    [Fact]
    public void AddReceiverServices_RegistersBackgroundService_WhenEnabled()
    {
        var services = new ServiceCollection();

        services.AddReceiverServices(enableBackgroundProcessor: true);

        var descriptor = services.SingleOrDefault(service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(ServiceBusProcessingBackgroundService));
        Assert.NotNull(descriptor);
    }
}
