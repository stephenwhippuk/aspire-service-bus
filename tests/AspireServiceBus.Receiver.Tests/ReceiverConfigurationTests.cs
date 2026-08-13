using Microsoft.Extensions.Configuration;

namespace AspireServiceBus.Receiver.Tests;

public class ReceiverConfigurationTests
{
    [Fact]
    public void ResolveServiceBusConnectionString_PrefersExplicitConnectionStringsSetting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings__servicebus"] = "Endpoint=sb://from-config:5672;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;",
                ["AzureWebJobsServiceBus"] = "Endpoint=sb://from-env:5672;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;"
            })
            .Build();

        var result = AspireServiceBus.Receiver.ReceiverConfiguration.ResolveServiceBusConnectionString(configuration);

        Assert.Equal("Endpoint=sb://from-config:5672;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;", result);
    }

    [Fact]
    public void ResolveQueueName_UsesServiceBusQueueNameSetting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceBus__QueueName"] = "custom-queue"
            })
            .Build();

        var result = AspireServiceBus.Receiver.ReceiverConfiguration.ResolveQueueName(configuration);

        Assert.Equal("custom-queue", result);
    }
}
