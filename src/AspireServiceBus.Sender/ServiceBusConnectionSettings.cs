using Microsoft.Extensions.Configuration;

namespace AspireServiceBus.Sender;

public sealed record ServiceBusConnectionDiagnostics(
    string QueueName,
    string? ResolvedConnectionString,
    bool HasResolvedConnectionString,
    string? ExplicitConnectionString,
    string? ConfiguredConnectionString,
    string? HistoryFilePath,
    string? LocalLogPath);

public static class ServiceBusConnectionSettings
{
    public static string? ResolveConnectionString(IConfiguration configuration)
    {
        return CreateDiagnostics(configuration, queueName: null, historyFilePath: null, localLogPath: null).ResolvedConnectionString;
    }

    public static ServiceBusConnectionDiagnostics CreateDiagnostics(IConfiguration configuration, string? queueName, string? historyFilePath, string? localLogPath)
    {
        var explicitConnectionString = configuration["ConnectionStrings__servicebus"]
            ?? configuration["ConnectionStrings:servicebus"];

        var configuredConnectionString = configuration.GetConnectionString("servicebus");
        var resolvedConnectionString = !string.IsNullOrWhiteSpace(explicitConnectionString)
            ? explicitConnectionString
            : string.IsNullOrWhiteSpace(configuredConnectionString) ? null : configuredConnectionString;

        return new ServiceBusConnectionDiagnostics(
            QueueName: queueName ?? "default-queue",
            ResolvedConnectionString: resolvedConnectionString,
            HasResolvedConnectionString: !string.IsNullOrWhiteSpace(resolvedConnectionString),
            ExplicitConnectionString: explicitConnectionString,
            ConfiguredConnectionString: configuredConnectionString,
            HistoryFilePath: historyFilePath,
            LocalLogPath: localLogPath);
    }
}
