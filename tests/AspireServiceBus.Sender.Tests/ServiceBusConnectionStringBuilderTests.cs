extern alias AppHost;

using AppHost::AspireServiceBus.AppHost;

namespace AspireServiceBus.Sender.Tests;

public class ServiceBusConnectionStringBuilderTests
{
    [Fact]
    public void BuildConnectionString_RetriesUntilPortIsAvailable()
    {
        var attemptCount = 0;

        var connectionString = ServiceBusConnectionStringBuilder.BuildConnectionString(
            hostName: "localhost",
            initialPort: null,
            resolvePort: () =>
            {
                attemptCount++;
                return attemptCount >= 3 ? "32836" : null;
            });

        Assert.Equal(
            "Endpoint=sb://127.0.0.1:32836;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            connectionString);
        Assert.Equal(3, attemptCount);
    }
}
