using AspireServiceBus.Receiver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspireServiceBus.Receiver.Tests;

public class ReceiverServiceRegistrationTests
{
    [Fact]
    public void AddReceiverServices_DoesNotRegisterBackgroundService()
    {
        var services = new ServiceCollection();

        services.AddReceiverServices();

        var descriptor = services.SingleOrDefault(service => service.ServiceType == typeof(ServiceBusProcessingBackgroundService));
        Assert.Null(descriptor);
    }

    [Fact]
    public void ReceivedMessageEnvelope_StoresMessageIdBodyAndHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["trace-id"] = "abc-123"
        };

        var envelope = new ReceivedMessageEnvelope("msg-1", "{\"hello\":\"world\"}", headers);

        Assert.Equal("msg-1", envelope.MessageId);
        Assert.Equal("{\"hello\":\"world\"}", envelope.Body);
        Assert.Equal(headers, envelope.Headers);
    }
}
