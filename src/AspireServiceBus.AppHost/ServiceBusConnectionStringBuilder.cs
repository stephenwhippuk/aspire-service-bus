namespace AspireServiceBus.AppHost;

public static class ServiceBusConnectionStringBuilder
{
    private const int DefaultEmulatorHostPort = 5672;

    public static string BuildConnectionStringForResource(object? resource, Func<string?>? resolveHost = null, Func<string?>? resolvePort = null)
    {
        string? hostName = null;
        string? initialPort = null;

        if (resource is not null)
        {
            hostName = TryGetStringValue(resource.GetType().GetProperty("HostName")?.GetValue(resource));
            initialPort = TryGetStringValue(resource.GetType().GetProperty("Port")?.GetValue(resource));
        }

        return BuildConnectionString(hostName ?? "127.0.0.1", initialPort, resolveHost, resolvePort);
    }

    public static string BuildConnectionString(
        string? hostName,
        string? initialPort,
        Func<string?>? resolveHost = null,
        Func<string?>? resolvePort = null,
        int retryAttempts = 20,
        TimeSpan? retryDelay = null)
    {
        var effectiveHost = ResolveEffectiveHost(resolveHost, retryAttempts, retryDelay);
        if (string.IsNullOrWhiteSpace(effectiveHost))
        {
            effectiveHost = NormalizeHost(hostName);
        }

        var effectivePort = ResolveEffectivePort(resolvePort, retryAttempts, retryDelay);
        if (string.IsNullOrWhiteSpace(effectivePort))
        {
            effectivePort = NormalizePort(initialPort);
        }

        if (string.IsNullOrWhiteSpace(effectivePort))
        {
            effectivePort = DefaultEmulatorHostPort.ToString();
        }

        var endpoint = $"Endpoint=sb://{effectiveHost}:{effectivePort};";
        return $"{endpoint}SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    }

    private static string? ResolveEffectiveHost(Func<string?>? resolveHost, int retryAttempts, TimeSpan? retryDelay)
    {
        if (resolveHost is null)
        {
            return null;
        }

        var delay = retryDelay ?? TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt < retryAttempts; attempt++)
        {
            var resolvedHost = NormalizeHost(resolveHost());
            if (!string.IsNullOrWhiteSpace(resolvedHost))
            {
                return resolvedHost;
            }

            if (attempt < retryAttempts - 1)
            {
                Thread.Sleep(delay);
            }
        }

        return null;
    }

    private static string? ResolveEffectivePort(Func<string?>? resolvePort, int retryAttempts, TimeSpan? retryDelay)
    {
        if (resolvePort is null)
        {
            return null;
        }

        var delay = retryDelay ?? TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt < retryAttempts; attempt++)
        {
            var resolvedPort = NormalizePort(resolvePort());
            if (!string.IsNullOrWhiteSpace(resolvedPort))
            {
                return resolvedPort;
            }

            if (attempt < retryAttempts - 1)
            {
                Thread.Sleep(delay);
            }
        }

        return null;
    }

    private static string NormalizeHost(string? hostName)
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            return "127.0.0.1";
        }

        return string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase)
            ? "127.0.0.1"
            : hostName;
    }

    private static string? TryGetStringValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        var valueText = value.ToString();
        return string.IsNullOrWhiteSpace(valueText) || valueText.Contains("ReferenceExpression", StringComparison.Ordinal)
            ? null
            : valueText;
    }

    private static string? NormalizePort(string? port)
    {
        if (string.IsNullOrWhiteSpace(port))
        {
            return null;
        }

        if (int.TryParse(port, out var numericPort) && numericPort > 0 && numericPort <= 65535)
        {
            return numericPort.ToString();
        }

        return null;
    }
}
