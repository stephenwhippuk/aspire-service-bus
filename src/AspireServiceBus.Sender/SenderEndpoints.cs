using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AspireServiceBus.Sender;

public static class SenderEndpoints
{
    private static readonly SemaphoreSlim LogWriteLock = new(1, 1);

    private sealed class JsonHttpResult(object? value, int statusCode) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var options = httpContext.RequestServices.GetService<IOptions<JsonOptions>>();
            var serializerOptions = options?.Value.SerializerOptions ?? new JsonOptions().SerializerOptions;

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json; charset=utf-8";

            var payload = JsonSerializer.SerializeToUtf8Bytes(value, serializerOptions);
            await httpContext.Response.Body.WriteAsync(payload, httpContext.RequestAborted);
        }
    }

    public static IEndpointRouteBuilder MapSenderEndpoints(this IEndpointRouteBuilder app, string queueName, string? localLogPath)
    {
        app.MapGet("/explorer/entities", (IExplorerEntityCatalog explorerCatalog) =>
        {
            return new JsonHttpResult(explorerCatalog.GetResponse(), StatusCodes.Status200OK);
        });

        app.MapGet("/history/success", async (IMessageHistoryStore historyStore, CancellationToken cancellationToken, int take = 20, int skip = 0) =>
        {
            var history = await historyStore.GetByOutcomeAsync(MessageHistoryOutcome.Success, take == 0 ? 50 : take, skip, cancellationToken);
            return new JsonHttpResult(history, StatusCodes.Status200OK);
        });

        app.MapGet("/history/failed", async (IMessageHistoryStore historyStore, CancellationToken cancellationToken, int take = 20, int skip = 0) =>
        {
            var history = await historyStore.GetByOutcomeAsync(MessageHistoryOutcome.Failed, take == 0 ? 50 : take, skip, cancellationToken);
            return new JsonHttpResult(history, StatusCodes.Status200OK);
        });

        app.MapDelete("/history/success", async (IMessageHistoryStore historyStore, CancellationToken cancellationToken) =>
        {
            await historyStore.PurgeByOutcomeAsync(MessageHistoryOutcome.Success, cancellationToken);
            return new JsonHttpResult(new { status = "purged", outcome = MessageHistoryOutcome.Success }, StatusCodes.Status200OK);
        });

        app.MapDelete("/history/failed", async (IMessageHistoryStore historyStore, CancellationToken cancellationToken) =>
        {
            await historyStore.PurgeByOutcomeAsync(MessageHistoryOutcome.Failed, cancellationToken);
            return new JsonHttpResult(new { status = "purged", outcome = MessageHistoryOutcome.Failed }, StatusCodes.Status200OK);
        });

        app.MapPost("/send", async (HttpContext httpContext, SendMessageRequest request, CancellationToken cancellationToken) =>
        {
            var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SenderEndpoints");
            var client = httpContext.RequestServices.GetService<ServiceBusClient>();
            var historyStore = httpContext.RequestServices.GetRequiredService<IMessageHistoryStore>();

            logger.LogDebug("Received send request for queue {QueueName} with payload {@Payload}", queueName, request);
            return await ExecuteSendAsync(logger, historyStore, client, queueName, localLogPath, request, cancellationToken);
        });

        app.MapPost("/history/{id}/resend", async (string id, HttpContext httpContext, ResendHistoryRequest? request, CancellationToken cancellationToken) =>
        {
            var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SenderEndpoints");
            var client = httpContext.RequestServices.GetService<ServiceBusClient>();
            var historyStore = httpContext.RequestServices.GetRequiredService<IMessageHistoryStore>();
            var existingEntry = await historyStore.GetByIdAsync(id, cancellationToken);
            if (existingEntry is null)
            {
                return new JsonHttpResult(new { error = "History entry not found." }, StatusCodes.Status404NotFound);
            }

            var resendRequest = CreateSendRequestFromHistory(existingEntry, request);
            return await ExecuteSendAsync(logger, historyStore, client, queueName, localLogPath, resendRequest, cancellationToken, id);
        });

        return app;
    }

    private static async Task<IResult> ExecuteSendAsync(
        ILogger logger,
        IMessageHistoryStore historyStore,
        ServiceBusClient? client,
        string queueName,
        string? localLogPath,
        SendMessageRequest request,
        CancellationToken cancellationToken,
        string? sourceAttemptId = null)
    {
        var effectiveHeaders = BuildEffectiveHeaders(request);

        var validationError = SendMessageRequestValidator.Validate(request);
        if (validationError is not null)
        {
            await historyStore.AppendAsync(CreateHistoryEntry(
                queueName,
                MessageHistoryOutcome.Failed,
                request,
                effectiveHeaders,
                request.BodyJson,
                failureReason: validationError,
                sourceAttemptId: sourceAttemptId), cancellationToken);

            return new JsonHttpResult(new { error = validationError }, StatusCodes.Status400BadRequest);
        }

        if (client is null)
        {
            const string unavailableMessage = "Service Bus connection is not available yet. The service will stay running and retry once the emulator connection is configured.";

            logger.LogWarning("Service Bus client unavailable for queue {QueueName}. Returning 503 with request {@Request}", queueName, request);

            await historyStore.AppendAsync(CreateHistoryEntry(
                queueName,
                MessageHistoryOutcome.Failed,
                request,
                effectiveHeaders,
                request.BodyJson,
                failureReason: unavailableMessage,
                sourceAttemptId: sourceAttemptId), cancellationToken);

            await AppendLocalLogAsync(localLogPath, new
            {
                timestamp = DateTimeOffset.UtcNow,
                service = "sender",
                action = "send-failed",
                queue = queueName,
                error = unavailableMessage
            }, cancellationToken);

            return new JsonHttpResult(new { error = unavailableMessage }, StatusCodes.Status503ServiceUnavailable);
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

            logger.LogInformation("Sending message to queue {QueueName} with message id {MessageId}", queueName, message.MessageId);
            logger.LogDebug("Send request details for queue {QueueName}: {@MessageProperties}", queueName, message.ApplicationProperties);

            await sender.SendMessageAsync(message, cancellationToken);

            await historyStore.AppendAsync(CreateHistoryEntry(
                queueName,
                MessageHistoryOutcome.Processing,
                request,
                effectiveHeaders,
                request.BodyJson,
                serviceBusMessageId: message.MessageId,
                sourceAttemptId: sourceAttemptId), cancellationToken);

            await AppendLocalLogAsync(localLogPath, new
            {
                timestamp = DateTimeOffset.UtcNow,
                service = "sender",
                action = "send",
                queue = queueName,
                messageId = message.MessageId,
                headers = message.ApplicationProperties
            }, cancellationToken);

            return new JsonHttpResult(new { status = "processing", queue = queueName, messageId = message.MessageId }, StatusCodes.Status202Accepted);
        }
        catch (ServiceBusException ex) when (IsTransientServiceBusFailure(ex))
        {
            const string transientFailureMessage = "Unable to reach the Service Bus emulator. Verify that the emulator is running and the connection is available.";

            logger.LogWarning(ex, "Transient Service Bus failure while sending to queue {QueueName}", queueName);

            await historyStore.AppendAsync(CreateHistoryEntry(
                queueName,
                MessageHistoryOutcome.Failed,
                request,
                effectiveHeaders,
                request.BodyJson,
                failureReason: transientFailureMessage,
                sourceAttemptId: sourceAttemptId), cancellationToken);

            await AppendLocalLogAsync(localLogPath, new
            {
                timestamp = DateTimeOffset.UtcNow,
                service = "sender",
                action = "send-failed",
                queue = queueName,
                error = transientFailureMessage
            }, cancellationToken);

            return new JsonHttpResult(
                new { error = transientFailureMessage },
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            var clientErrorMessage = CreateClientErrorMessage(ex);

            logger.LogError(ex, "Failed to send message to queue {QueueName}", queueName);

            await historyStore.AppendAsync(CreateHistoryEntry(
                queueName,
                MessageHistoryOutcome.Failed,
                request,
                effectiveHeaders,
                request.BodyJson,
                failureReason: clientErrorMessage,
                sourceAttemptId: sourceAttemptId), cancellationToken);

            await AppendLocalLogAsync(localLogPath, new
            {
                timestamp = DateTimeOffset.UtcNow,
                service = "sender",
                action = "send-failed",
                queue = queueName,
                error = clientErrorMessage,
                exception = ex.ToString()
            }, cancellationToken);

            return new JsonHttpResult(new { error = clientErrorMessage }, StatusCodes.Status500InternalServerError);
        }
    }

    private static MessageHistoryEntry CreateHistoryEntry(
        string queueName,
        string outcome,
        SendMessageRequest request,
        IReadOnlyDictionary<string, string> effectiveHeaders,
        string bodyJson,
        string? failureReason = null,
        string? serviceBusMessageId = null,
        string? sourceAttemptId = null)
    {
        return new MessageHistoryEntry(
            Id: Guid.NewGuid().ToString("N"),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            QueueName: queueName,
            Outcome: outcome,
            FailureReason: failureReason,
            ServiceBusMessageId: serviceBusMessageId,
            Request: request,
            EffectiveHeaders: effectiveHeaders,
            BodyJson: bodyJson,
            SourceAttemptId: sourceAttemptId,
            IsResend: !string.IsNullOrWhiteSpace(sourceAttemptId));
    }

    private static SendMessageRequest CreateSendRequestFromHistory(MessageHistoryEntry entry, ResendHistoryRequest? request)
    {
        return new SendMessageRequest(
            request?.Timestamp ?? entry.Request.Timestamp,
            request?.EntityName ?? entry.Request.EntityName,
            request?.TargetApplication ?? entry.Request.TargetApplication,
            request?.BodyJson ?? entry.BodyJson,
            request?.CustomHeaders ?? entry.Request.CustomHeaders?.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
            null);
    }

    private static IReadOnlyDictionary<string, string> BuildEffectiveHeaders(SendMessageRequest request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["timestamp"] = request.Timestamp,
            ["entity-name"] = request.EntityName,
            ["target-application"] = request.TargetApplication
        };

        if (request.CustomHeaders is not null)
        {
            foreach (var (key, value) in request.CustomHeaders)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    headers[key] = value;
                }
            }
        }

        return headers;
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
    Dictionary<string, string>? CustomHeaders,
    string? EntityType = null);

public sealed record ResendHistoryRequest(
    string? Timestamp,
    string? EntityName,
    string? TargetApplication,
    string? BodyJson,
    Dictionary<string, string>? CustomHeaders,
    string? EntityType = null);
