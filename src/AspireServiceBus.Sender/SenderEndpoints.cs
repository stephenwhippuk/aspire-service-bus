using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace AspireServiceBus.Sender;

public static class SenderEndpoints
{
    private static readonly SemaphoreSlim LogWriteLock = new(1, 1);

    public static IEndpointRouteBuilder MapSenderEndpoints(this IEndpointRouteBuilder app, string queueName, string? localLogPath)
    {
        app.MapPost("/send", async (HttpContext httpContext, SendMessageRequest request, CancellationToken cancellationToken) =>
        {
            var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SenderEndpoints");
            var client = httpContext.RequestServices.GetService<ServiceBusClient>();

            var validationError = SendMessageRequestValidator.Validate(request);
            if (validationError is not null)
            {
                return Results.BadRequest(new { error = validationError });
            }

            if (client is null)
            {
                await AppendLocalLogAsync(localLogPath, new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    service = "sender",
                    action = "send-failed",
                    queue = queueName,
                    error = "Service Bus connection is not available yet. The service will stay running and retry once the emulator connection is configured."
                }, cancellationToken);

                return Results.Json(new { error = "Service Bus connection is not available yet. The service will stay running and retry once the emulator connection is configured." }, statusCode: 503);
            }

            try
            {
                await using var sender = client.CreateSender(queueName);

                var message = new ServiceBusMessage(request.BodyJson)
                {
                    ContentType = "application/json",
                    MessageId = Guid.NewGuid().ToString("N")
                };

                message.ApplicationProperties["timestamp"] = request.Timestamp;
                message.ApplicationProperties["entity-name"] = request.EntityName;
                message.ApplicationProperties["target-application"] = request.TargetApplication;

                if (request.CustomHeaders is not null)
                {
                    foreach (var (key, value) in request.CustomHeaders)
                    {
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                        {
                            message.ApplicationProperties[key] = value;
                        }
                    }
                }

                await sender.SendMessageAsync(message, cancellationToken);
                await AppendLocalLogAsync(localLogPath, new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    service = "sender",
                    action = "send",
                    queue = queueName,
                    messageId = message.MessageId,
                    headers = message.ApplicationProperties
                }, cancellationToken);

                return Results.Ok(new { status = "sent", queue = queueName, messageId = message.MessageId });
            }
            catch (ServiceBusException ex) when (IsTransientServiceBusFailure(ex))
            {
                await AppendLocalLogAsync(localLogPath, new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    service = "sender",
                    action = "send-failed",
                    queue = queueName,
                    error = "Unable to reach the Service Bus emulator. Verify that the emulator is running and the connection is available."
                }, cancellationToken);

                return Results.Json(
                    new { error = "Unable to reach the Service Bus emulator. Verify that the emulator is running and the connection is available." },
                    statusCode: 503);
            }
            catch (Exception ex)
            {
                var clientErrorMessage = CreateClientErrorMessage(ex);

                logger.LogError(ex, "Failed to send message to queue {QueueName}", queueName);

                await AppendLocalLogAsync(localLogPath, new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    service = "sender",
                    action = "send-failed",
                    queue = queueName,
                    error = clientErrorMessage,
                    exception = ex.ToString()
                }, cancellationToken);

                return Results.Json(new { error = clientErrorMessage }, statusCode: 500);
            }
        });

        return app;
    }

    public static string CreateClientErrorMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return "An unexpected error occurred while sending the message.";
    }

    private static bool IsTransientServiceBusFailure(ServiceBusException ex)
    {
        return ex.IsTransient || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AppendLocalLogAsync(string? logFilePath, object payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            return;
        }

        await LogWriteLock.WaitAsync(cancellationToken);

        try
        {
            var directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.AppendAllTextAsync(logFilePath, JsonSerializer.Serialize(payload) + Environment.NewLine, cancellationToken);
        }
        finally
        {
            LogWriteLock.Release();
        }
    }
}

public sealed record SendMessageRequest(
    string Timestamp,
    string EntityName,
    string TargetApplication,
    string BodyJson,
    Dictionary<string, string>? CustomHeaders);
