extern alias AppHost;

using AppHost::AspireServiceBus.AppHost;

namespace AspireServiceBus.Sender.Tests;

public class AppHostConnectionStringTests
{
    [Fact]
    public void BuildConnectionStringForResource_UsesResolvedPortWhenMetadataIsMissing()
    {
        var resource = new FakeResource();

        var connectionString = ServiceBusConnectionStringBuilder.BuildConnectionStringForResource(
            resource,
            resolvePort: () => "32785");

        Assert.Contains("Endpoint=sb://127.0.0.1:32785;", connectionString);
    }

    [Fact]
    public void BuildConnectionStringForResource_PrefersResolvedPortOverMetadataPort()
    {
        var resource = new FakeResource { Port = "39500" };

        var connectionString = ServiceBusConnectionStringBuilder.BuildConnectionStringForResource(
            resource,
            resolvePort: () => "32824");

        Assert.Contains("Endpoint=sb://127.0.0.1:32824;", connectionString);
    }

    [Fact]
    public void BuildConnectionStringForResource_UsesResolvedHostAndPort()
    {
        var resource = new FakeResource();

        var connectionString = ServiceBusConnectionStringBuilder.BuildConnectionStringForResource(
            resource,
            resolveHost: () => "172.23.0.3",
            resolvePort: () => "5672");

        Assert.Contains("Endpoint=sb://172.23.0.3:5672;", connectionString);
    }

    private sealed class FakeResource
    {
        public string? HostName => null;

        public string? Port { get; set; }
    }
}
